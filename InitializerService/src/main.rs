use std::env;
use std::error::Error;
use std::io::{Read, Write};
use std::net::{TcpStream, ToSocketAddrs};
use std::path::{Path, PathBuf};
use std::process::{Child, Command, Stdio};
use std::thread;
use std::time::{Duration, Instant};

const DEFAULT_OBS_PATH: &str = r"C:\Program Files\obs-studio\bin\64bit\obs64.exe";
const DEFAULT_VDJ_PATH: &str = r"C:\Program Files\VirtualDJ\virtualdj.exe";
const DEFAULT_PERSISTENCE_HEALTH_URL: &str = "http://127.0.0.1:8919/health";
const DEFAULT_ORCHESTRATION_HEALTH_URL: &str = "http://127.0.0.1:8111/health";
const HEALTH_TIMEOUT: Duration = Duration::from_secs(300);
const HEALTH_RETRY_INTERVAL: Duration = Duration::from_secs(1);
const HEALTH_PROBE_TIMEOUT: Duration = Duration::from_secs(2);

fn main() {
    if let Err(err) = run() {
        eprintln!("initializer failed: {err}");
        std::process::exit(1);
    }
}

fn run() -> Result<(), Box<dyn Error>> {
    let repo_root = find_repo_root()?;
    let no_vdj = env::args().any(|arg| arg.eq_ignore_ascii_case("--NoVDJ"));

    let obs_path = env::var("OBS_PATH").unwrap_or_else(|_| DEFAULT_OBS_PATH.to_string());
    let vdj_path = env::var("VDJ_PATH").unwrap_or_else(|_| DEFAULT_VDJ_PATH.to_string());
    let persistence_health =
        env::var("PERSISTENCE_HEALTH_URL").unwrap_or_else(|_| DEFAULT_PERSISTENCE_HEALTH_URL.to_string());
    let orchestration_health =
        env::var("ORCHESTRATION_HEALTH_URL").unwrap_or_else(|_| DEFAULT_ORCHESTRATION_HEALTH_URL.to_string());


    let mut children: Vec<Child> = Vec::new();

    let persistence_dir = repo_root.join("PersistenceService");
    children.push(spawn_process(
        "persistence service",
        "cmd",
        &["/C", "gradlew.bat", "clean", "build", "bootRun"],
        &persistence_dir,
    )?);

    if let Some(child) = try_spawn_optional_binary("OBS", &obs_path)? {
        children.push(child);
    }

    if !no_vdj {
        if let Some(child) = try_spawn_optional_binary("VirtualDJ", &vdj_path)? {
            children.push(child);
        }
    } else {
        println!("Skipping VirtualDJ (--NoVDJ)");
    }

    wait_for_health("persistence service", &persistence_health)?;

    let customizer_dir = repo_root.join("customizer-interface");
    children.push(spawn_process(
        "customizer-interface",
        "cmd",
        &[
            "/C",
            "npm",
            "install",
            "&&",
            "npm",
            "run",
            "build",
            "&&",
            "npm",
            "start",
        ],
        &customizer_dir,
    )?);

    let orchestration_dir = repo_root.join("OrchestrationService");
    run_blocking_command("orchestration clean", "go", &["clean"], &orchestration_dir)?;
    run_blocking_command(
        "orchestration build",
        "go",
        &["build", "-o", "OrchestrationService.exe", "main.go"],
        &orchestration_dir,
    )?;
    children.push(spawn_process(
        "orchestration service",
        "OrchestrationService.exe",
        &[],
        &orchestration_dir,
    )?);

    wait_for_health("orchestration service", &orchestration_health)?;

    println!("All services launched successfully.");

    drop(children);
    Ok(())
}

fn find_repo_root() -> Result<PathBuf, Box<dyn Error>> {
    let mut current = env::current_dir()?;
    loop {
        if current.join("RunAllLocal.ps1").exists() {
            return Ok(current);
        }
        if !current.pop() {
            break;
        }
    }
    Err("could not locate repository root containing RunAllLocal.ps1".into())
}

fn try_spawn_optional_binary(name: &str, binary_path: &str) -> Result<Option<Child>, Box<dyn Error>> {
    let path = PathBuf::from(binary_path);
    if !path.exists() {
        eprintln!("Skipping {name}: path not found ({binary_path})");
        return Ok(None);
    }

    let child = Command::new(path)
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .spawn()?;
    println!("Started {name}");
    Ok(Some(child))
}

fn spawn_process(
    name: &str,
    program: &str,
    args: &[&str],
    current_dir: &Path,
) -> Result<Child, Box<dyn Error>> {
    let child = Command::new(program)
        .args(args)
        .current_dir(current_dir)
        .spawn()?;
    println!("Started {name}");
    Ok(child)
}

fn run_blocking_command(
    name: &str,
    program: &str,
    args: &[&str],
    current_dir: &Path,
) -> Result<(), Box<dyn Error>> {
    let status = Command::new(program)
        .args(args)
        .current_dir(current_dir)
        .status()?;
    if status.success() {
        Ok(())
    } else {
        Err(format!("{name} failed with status {status}").into())
    }
}

fn wait_for_health(name: &str, url: &str) -> Result<(), Box<dyn Error>> {
    println!("Waiting for {name} health at {url} ...");
    let start = Instant::now();

    loop {
        if let Ok(status) = probe_http_status(url) {
            if (200..300).contains(&status) || status == 404 {
                println!("{name} is reachable at {url} (HTTP {status})");
                return Ok(());
            }
        }

        if start.elapsed() > HEALTH_TIMEOUT {
            return Err(format!("timed out waiting for {name} health at {url}").into());
        }
        thread::sleep(HEALTH_RETRY_INTERVAL);
    }
}

fn probe_http_status(url: &str) -> Result<u16, Box<dyn Error>> {
    let (host, port, path) = parse_http_url(url)?;
    let mut addrs = (host.as_str(), port).to_socket_addrs()?;
    let addr = addrs.next().ok_or("unable to resolve health host")?;

    let mut stream = TcpStream::connect_timeout(&addr, HEALTH_PROBE_TIMEOUT)?;
    stream.set_read_timeout(Some(HEALTH_PROBE_TIMEOUT))?;
    stream.set_write_timeout(Some(HEALTH_PROBE_TIMEOUT))?;

    let request = format!("GET {path} HTTP/1.1\r\nHost: {host}\r\nConnection: close\r\n\r\n");
    stream.write_all(request.as_bytes())?;

    let mut buffer = [0_u8; 512];
    let bytes_read = stream.read(&mut buffer)?;
    if bytes_read == 0 {
        return Err("empty response from health endpoint".into());
    }

    let response = String::from_utf8_lossy(&buffer[..bytes_read]);
    let status_line = response.lines().next().ok_or("missing status line")?;
    let mut parts = status_line.split_whitespace();
    let _http_version = parts.next().ok_or("missing http version")?;
    let status_code = parts
        .next()
        .ok_or("missing status code")?
        .parse::<u16>()?;
    Ok(status_code)
}

fn parse_http_url(url: &str) -> Result<(String, u16, String), Box<dyn Error>> {
    let raw = url.trim();
    let without_scheme = raw
        .strip_prefix("http://")
        .ok_or("only http:// health urls are supported")?;

    let (host_port, path) = match without_scheme.split_once('/') {
        Some((host_port, path)) => (host_port, format!("/{path}")),
        None => (without_scheme, "/".to_string()),
    };

    let (host, port) = match host_port.rsplit_once(':') {
        Some((host, port)) => (host.to_string(), port.parse::<u16>()?),
        None => (host_port.to_string(), 80),
    };

    if host.is_empty() {
        return Err("health url host is empty".into());
    }

    Ok((host, port, path))
}
