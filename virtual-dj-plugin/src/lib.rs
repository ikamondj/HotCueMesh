use serde::{Deserialize, Serialize};
use std::collections::HashSet;
use std::io::Write;
use std::net::TcpStream;
use std::sync::{Arc, Mutex};
use std::thread;
use std::time::{Duration, Instant};
use virtualdj_plugin_sdk::{DspPlugin, PluginBase, PluginError, PluginInfo, Result, ffi};

/// Configuration for TCP retry behavior
#[derive(Clone, Debug)]
pub struct TcpConfig {
    /// Host to connect to
    pub host: String,
    /// Port to connect to
    pub port: u16,
    /// Cooldown duration after failed connection attempt (milliseconds)
    pub retry_cooldown_ms: u64,
    /// Maximum retries before giving up
    pub max_retries: u32,
}

impl Default for TcpConfig {
    fn default() -> Self {
        Self {
            host: "127.0.0.1".to_string(),
            port: 8112,
            retry_cooldown_ms: 5000,
            max_retries: 3,
        }
    }
}

/// Represents a hotcue event to be sent as JSON
#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct HotcueEvent {
    pub title: String,
    pub cue: String,
    pub deck: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub cue_name: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub commands: Option<Vec<Command>>,
}

/// Command to be executed (e.g., variable changes, show/hide)
#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct Command {
    #[serde(rename = "type")]
    pub command_type: String,
    pub name: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub value: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub source: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub filter: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub add: Option<bool>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub rand: Option<bool>,
}

/// Master deck event
#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct MasterEvent {
    pub title: String,
    pub cue: String,
}

/// Root event payload
#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct EventPayload {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub master: Option<MasterEvent>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub cues: Option<Vec<HotcueEvent>>,
}

/// Thread-safe handle to VirtualDJ context with raw pointers
/// This can be safely sent between threads as the pointers are valid for the plugin lifetime
#[derive(Copy, Clone)]
pub struct VdjContextHandle {
    plugin: *mut ffi::VdjPlugin,
    callbacks: *const ffi::VdjCallbacks,
}

impl VdjContextHandle {
    pub fn new(plugin: *mut ffi::VdjPlugin, callbacks: *const ffi::VdjCallbacks) -> Self {
        VdjContextHandle { plugin, callbacks }
    }

    /// Query VirtualDJ for a numeric value
    pub fn get_info_double(&self, command: &str) -> Result<f64> {
        if self.plugin.is_null() || self.callbacks.is_null() {
            return Err(PluginError::NullPointer);
        }

        let c_command = std::ffi::CString::new(command)
            .map_err(|_| PluginError::Fail)?;

        let mut result: f64 = 0.0;

        let hr = unsafe {
            ((*self.callbacks).get_info)(
                self.plugin,
                c_command.as_ptr() as *const u8,
                &mut result as *mut f64,
            )
        };

        if hr == ffi::S_OK {
            Ok(result)
        } else {
            Err(PluginError::from(hr))
        }
    }

    /// Query VirtualDJ for a string value
    pub fn get_info_string(&self, command: &str) -> Result<String> {
        if self.plugin.is_null() || self.callbacks.is_null() {
            return Err(PluginError::NullPointer);
        }

        let c_command = std::ffi::CString::new(command)
            .map_err(|_| PluginError::Fail)?;

        let mut output: [u8; 4096] = [0; 4096];

        let hr = unsafe {
            ((*self.callbacks).get_string_info)(
                self.plugin,
                c_command.as_ptr() as *const u8,
                output.as_mut_ptr(),
                output.len() as i32,
            )
        };

        if hr == ffi::S_OK {
            let len = if let Some(pos) = output.iter().position(|&b| b == 0) {
                pos
            } else {
                output.len()
            };
            String::from_utf8(output[..len].to_vec()).map_err(|_| PluginError::Fail)
        } else {
            Err(PluginError::from(hr))
        }
    }
}

// SAFETY: Raw pointers are Send/Sync if they come from a valid source
// The pointers are valid for the entire plugin lifetime
unsafe impl Send for VdjContextHandle {}
unsafe impl Sync for VdjContextHandle {}

/// TCP client with retry logic and configurable cooldown
pub struct TcpClient {
    config: TcpConfig,
    last_connect_attempt: Arc<Mutex<Instant>>,
    connect_attempts: Arc<Mutex<u32>>,
}

impl TcpClient {
    pub fn new(config: TcpConfig) -> Self {
        Self {
            config,
            last_connect_attempt: Arc::new(Mutex::new(Instant::now())),
            connect_attempts: Arc::new(Mutex::new(0)),
        }
    }

    /// Send a message with retry logic and configurable cooldown
    pub fn send_with_retry(&self, message: &str) -> Result<()> {
        let mut attempts = self.connect_attempts.lock().unwrap();
        let mut last_attempt = self.last_connect_attempt.lock().unwrap();

        // Check if we're still in cooldown
        if *attempts > 0 && last_attempt.elapsed() < Duration::from_millis(self.config.retry_cooldown_ms) {
            return Err(PluginError::Fail);
        }

        // Reset attempts if cooldown has passed
        if last_attempt.elapsed() >= Duration::from_millis(self.config.retry_cooldown_ms) {
            *attempts = 0;
        }

        // Try to connect and send
        for attempt in 0..self.config.max_retries {
            match self.send_internal(message) {
                Ok(_) => {
                    *attempts = 0;
                    *last_attempt = Instant::now();
                    return Ok(());
                }
                Err(_) => {
                    *attempts = attempt + 1;
                    *last_attempt = Instant::now();
                    if attempt < self.config.max_retries - 1 {
                        thread::sleep(Duration::from_millis(100));
                    }
                }
            }
        }

        Err(PluginError::Fail)
    }

    fn send_internal(&self, message: &str) -> Result<()> {
        let addr = format!("{}:{}", self.config.host, self.config.port);
        match TcpStream::connect(&addr) {
            Ok(mut stream) => {
                stream.write_all(message.as_bytes()).map_err(|_| PluginError::Fail)?;
                Ok(())
            }
            Err(_) => Err(PluginError::Fail),
        }
    }
}

/// Main hotcue event sender plugin
pub struct HotcuePlugin {
    tcp_client: Arc<TcpClient>,
    running: Arc<Mutex<bool>>,
    sent_cues: Arc<Mutex<HashSet<String>>>,
    last_master_title: Arc<Mutex<String>>,
    poll_thread: Arc<Mutex<Option<thread::JoinHandle<()>>>>,
    config: TcpConfig,
    vdj_context: Arc<Mutex<Option<VdjContextHandle>>>,
}

impl HotcuePlugin {
    pub fn new(config: TcpConfig) -> Self {
        Self {
            tcp_client: Arc::new(TcpClient::new(config.clone())),
            running: Arc::new(Mutex::new(false)),
            sent_cues: Arc::new(Mutex::new(HashSet::new())),
            last_master_title: Arc::new(Mutex::new(String::new())),
            poll_thread: Arc::new(Mutex::new(None)),
            config,
            vdj_context: Arc::new(Mutex::new(None)),
        }
    }

    /// Initialize the plugin with VirtualDJ context
    pub fn init(&mut self, plugin: *mut ffi::VdjPlugin, callbacks: *const ffi::VdjCallbacks) {
        *self.vdj_context.lock().unwrap() = Some(VdjContextHandle::new(plugin, callbacks));
    }

    /// Start the polling thread
    fn start_polling(&self) {
        let tcp_client = Arc::clone(&self.tcp_client);
        let running = Arc::clone(&self.running);
        let sent_cues = Arc::clone(&self.sent_cues);
        let last_master = Arc::clone(&self.last_master_title);
        let vdj_context = Arc::clone(&self.vdj_context);

        let handle = thread::spawn(move || {
            Self::poll_state_changes(tcp_client, running, sent_cues, last_master, vdj_context);
        });

        *self.poll_thread.lock().unwrap() = Some(handle);
    }

    /// Poll VirtualDJ state for hotcue changes
    fn poll_state_changes(
        tcp_client: Arc<TcpClient>,
        running: Arc<Mutex<bool>>,
        sent_cues: Arc<Mutex<HashSet<String>>>,
        last_master: Arc<Mutex<String>>,
        vdj_context: Arc<Mutex<Option<VdjContextHandle>>>,
    ) {
        while *running.lock().unwrap() {
            let context_opt = *vdj_context.lock().unwrap();
            if context_opt.is_none() {
                thread::sleep(Duration::from_millis(100));
                continue;
            }
            let context = context_opt.unwrap();

            let mut payload = EventPayload {
                master: None,
                cues: None,
            };

            // Poll master deck
            if let Some(master_event) = Self::get_master_event(&context, &last_master) {
                payload.master = Some(master_event);
            }

            // Poll all decks for hotcues
            if let Some(cues) = Self::get_deck_cues(&context, &sent_cues) {
                payload.cues = Some(cues);
            }

            // Send if we have any events
            if payload.master.is_some() || (payload.cues.is_some() && !payload.cues.as_ref().unwrap().is_empty()) {
                if let Ok(json) = serde_json::to_string(&payload) {
                    let _ = tcp_client.send_with_retry(&json);
                }
            }

            thread::sleep(Duration::from_millis(100));
        }
    }

    /// Get master deck event
    fn get_master_event(context: &VdjContextHandle, last_master: &Arc<Mutex<String>>) -> Option<MasterEvent> {
        let master_deck_str = context.get_info_string("get_activedeck").ok()?;
        let master_deck: i32 = master_deck_str.trim().parse().ok()?;

        if !(1..=4).contains(&master_deck) {
            return None;
        }

        let title = context
            .get_info_string(&format!("deck {} get_title", master_deck))
            .unwrap_or_default();

        if title.is_empty() {
            return None;
        }

        let mut last = last_master.lock().unwrap();
        if *last != title {
            *last = title.clone();
            Some(MasterEvent {
                title,
                cue: "master".to_string(),
            })
        } else {
            None
        }
    }

    /// Get hotcues from all decks
    fn get_deck_cues(context: &VdjContextHandle, sent_cues: &Arc<Mutex<HashSet<String>>>) -> Option<Vec<HotcueEvent>> {
        let mut cues = Vec::new();

        for deck in 1..=4 {
            let audible = context
                .get_info_string(&format!("deck {} is_audible", deck))
                .unwrap_or_default()
                .to_lowercase();

            if !matches!(audible.as_str(), "on" | "yes" | "true") {
                continue;
            }

            let title = context
                .get_info_string(&format!("deck {} get_title", deck))
                .unwrap_or_default();

            if title.is_empty() {
                continue;
            }

            let cursor_pos = context
                .get_info_double(&format!("deck {} get_position", deck))
                .unwrap_or(-1.0);

            if cursor_pos < 0.0 {
                continue;
            }

            let mut sent_guard = sent_cues.lock().unwrap();
            for cue_num in 1..=128 {
                let has_cue_str = context
                    .get_info_string(&format!("deck {} has_cue {}", deck, cue_num))
                    .unwrap_or_default()
                    .to_lowercase();

                if has_cue_str != "on" {
                    continue;
                }

                let cue_pos = match context.get_info_double(&format!("deck {} cue_pos {}", deck, cue_num)) {
                    Ok(pos) => pos,
                    Err(_) => continue,
                };

                if cue_pos < 0.0 {
                    continue;
                }

                let cue_key = format!("{}::{}", title, cue_num);

                if cursor_pos >= cue_pos {
                    if !sent_guard.contains(&cue_key) {
                        let cue_name = context
                            .get_info_string(&format!("deck {} cue_name {}", deck, cue_num))
                            .ok();

                        cues.push(HotcueEvent {
                            title: title.clone(),
                            cue: cue_num.to_string(),
                            deck: deck.to_string(),
                            cue_name,
                            commands: None,
                        });

                        sent_guard.insert(cue_key);
                    }
                } else {
                    sent_guard.remove(&cue_key);
                }
            }
        }

        if cues.is_empty() {
            None
        } else {
            Some(cues)
        }
    }
}

impl PluginBase for HotcuePlugin {
    fn on_load(&mut self) -> Result<()> {
        *self.running.lock().unwrap() = true;
        self.start_polling();
        Ok(())
    }

    fn get_info(&self) -> PluginInfo {
        PluginInfo {
            name: "Hotcue Event Sender".to_string(),
            author: "Ikamon".to_string(),
            description: "Sends VirtualDJ hotcue events as JSON over TCP to orchestration service".to_string(),
            version: "1.0.0".to_string(),
            flags: 0,
        }
    }
}

impl DspPlugin for HotcuePlugin {
    fn on_process_samples(&mut self, _buffer: &mut [f32]) -> Result<()> {
        Ok(())
    }
}

impl Drop for HotcuePlugin {
    fn drop(&mut self) {
        *self.running.lock().unwrap() = false;
        if let Some(handle) = self.poll_thread.lock().unwrap().take() {
            let _ = handle.join();
        }
    }
}

// Export FFI functions for C++ interop
#[no_mangle]
pub extern "C" fn create_plugin() -> *mut HotcuePlugin {
    Box::into_raw(Box::new(HotcuePlugin::new(TcpConfig::default())))
}

#[no_mangle]
pub extern "C" fn init_plugin(
    plugin: *mut HotcuePlugin,
    vdj_plugin: *mut ffi::VdjPlugin,
    callbacks: *const ffi::VdjCallbacks,
) {
    if !plugin.is_null() {
        unsafe {
            (*plugin).init(vdj_plugin, callbacks);
        }
    }
}

#[no_mangle]
pub extern "C" fn destroy_plugin(plugin: *mut HotcuePlugin) {
    if !plugin.is_null() {
        unsafe {
            let _ = Box::from_raw(plugin);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_hotcue_event_serialization() {
        let event = HotcueEvent {
            title: "Test Track".to_string(),
            cue: "1".to_string(),
            deck: "1".to_string(),
            cue_name: None,
            commands: None,
        };

        let json = serde_json::to_string(&event).unwrap();
        assert!(json.contains("Test Track"));
        assert!(json.contains("\"cue\":\"1\""));
    }

    #[test]
    fn test_tcp_config_default() {
        let config = TcpConfig::default();
        assert_eq!(config.host, "127.0.0.1");
        assert_eq!(config.port, 8112);
        assert_eq!(config.retry_cooldown_ms, 5000);
    }

    #[test]
    fn test_event_payload_serialization() {
        let payload = EventPayload {
            master: Some(MasterEvent {
                title: "Master".to_string(),
                cue: "master".to_string(),
            }),
            cues: None,
        };

        let json = serde_json::to_string(&payload).unwrap();
        assert!(json.contains("master"));
    }

    #[test]
    fn test_tcp_config_retry() {
        let config = TcpConfig {
            host: "localhost".to_string(),
            port: 9999,
            retry_cooldown_ms: 1000,
            max_retries: 3,
        };
        assert_eq!(config.max_retries, 3);
    }
}

