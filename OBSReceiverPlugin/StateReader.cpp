#include "StateReader.hpp"
#ifdef _WIN32
#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "Ws2_32.lib")
#endif
#include <atomic>
#include <climits>
#include <mutex>
#include <string>
#include <thread>
#include <utility>
#include <vector>
#include <obs-module.h>

namespace {
struct FilterNode {
    std::string name;
    std::string kind;
};

struct SourceNode {
    std::string name;
    std::string path;
    bool is_group = false;
    std::vector<FilterNode> filters;
    std::vector<SourceNode> sources;
};

struct SceneNode {
    std::string name;
    std::vector<SourceNode> sources;
};

std::atomic<bool> g_state_reader_stop{false};
std::thread g_state_reader_thread;

#ifdef _WIN32
std::mutex g_state_reader_socket_mutex;
SOCKET g_state_reader_listener = INVALID_SOCKET;
SOCKET g_state_reader_client = INVALID_SOCKET;
constexpr size_t kMaxHttpRequestSize = 16384;

static inline std::string safe_text(const char *value)
{
    return value ? std::string(value) : std::string();
}

static inline std::string build_source_path(const std::string &parent, const std::string &name)
{
    if (parent.empty()) {
        return name;
    }

    std::string path;
    path.reserve(parent.size() + 2 + name.size());
    path.append(parent);
    path.append("->");
    path.append(name);
    return path;
}

static void append_json_string(std::string &out, const std::string &value)
{
    static constexpr char hex[] = "0123456789abcdef";
    out.push_back('"');
    for (unsigned char c : value) {
        switch (c) {
        case '\"':
            out.append("\\\"");
            break;
        case '\\':
            out.append("\\\\");
            break;
        case '\b':
            out.append("\\b");
            break;
        case '\f':
            out.append("\\f");
            break;
        case '\n':
            out.append("\\n");
            break;
        case '\r':
            out.append("\\r");
            break;
        case '\t':
            out.append("\\t");
            break;
        default:
            if (c < 0x20) {
                out.append("\\u00");
                out.push_back(hex[c >> 4]);
                out.push_back(hex[c & 0x0F]);
            } else {
                out.push_back(static_cast<char>(c));
            }
            break;
        }
    }
    out.push_back('"');
}

static std::vector<FilterNode> collect_filters(obs_source_t *source)
{
    std::vector<FilterNode> filters;

    auto cb = [](obs_source_t *, obs_source_t *filter, void *param) {
        auto *out = static_cast<std::vector<FilterNode> *>(param);
        FilterNode node;
        node.name = safe_text(obs_source_get_name(filter));
        node.kind = safe_text(obs_source_get_id(filter));
        out->emplace_back(std::move(node));
    };

    obs_source_enum_filters(source, cb, &filters);
    return filters;
}

static void collect_scene_sources(obs_scene_t *scene, const std::string &parent_path, std::vector<SourceNode> &out);

static bool enum_scene_item(obs_scene_t *, obs_sceneitem_t *scene_item, void *param)
{
    auto *ctx = static_cast<std::pair<const std::string *, std::vector<SourceNode> *> *>(param);

    obs_source_t *source = obs_sceneitem_get_source(scene_item);
    if (!source) {
        return true;
    }

    SourceNode node;
    node.name = safe_text(obs_source_get_name(source));
    node.path = build_source_path(*ctx->first, node.name);
    node.is_group = obs_source_is_group(source);
    node.filters = collect_filters(source);

    if (node.is_group) {
        obs_scene_t *group_scene = obs_group_from_source(source);
        if (group_scene) {
            collect_scene_sources(group_scene, node.path, node.sources);
        }
    }

    ctx->second->emplace_back(std::move(node));
    return true;
}

static void collect_scene_sources(obs_scene_t *scene, const std::string &parent_path, std::vector<SourceNode> &out)
{
    std::pair<const std::string *, std::vector<SourceNode> *> ctx{&parent_path, &out};
    obs_scene_enum_items(scene, enum_scene_item, &ctx);
}

static bool enum_scene_source(void *param, obs_source_t *scene_source)
{
    if (!scene_source || obs_source_is_group(scene_source)) {
        return true;
    }

    obs_scene_t *scene = obs_scene_from_source(scene_source);
    if (!scene) {
        return true;
    }

    auto *out = static_cast<std::vector<SceneNode> *>(param);
    SceneNode node;
    node.name = safe_text(obs_source_get_name(scene_source));
    collect_scene_sources(scene, std::string(), node.sources);
    out->emplace_back(std::move(node));
    return true;
}

static std::vector<SceneNode> collect_state_tree()
{
    std::vector<SceneNode> scenes;
    obs_enum_scenes(enum_scene_source, &scenes);
    return scenes;
}

static void append_filter_json(std::string &out, const FilterNode &filter)
{
    out.append("{\"name\":");
    append_json_string(out, filter.name);
    out.append(",\"kind\":");
    append_json_string(out, filter.kind);
    out.push_back('}');
}

static void append_source_json(std::string &out, const SourceNode &source)
{
    out.append("{\"name\":");
    append_json_string(out, source.name);
    out.append(",\"path\":");
    append_json_string(out, source.path);
    out.append(",\"isGroup\":");
    out.append(source.is_group ? "true" : "false");

    out.append(",\"filters\":[");
    for (size_t i = 0; i < source.filters.size(); ++i) {
        if (i > 0) {
            out.push_back(',');
        }
        append_filter_json(out, source.filters[i]);
    }
    out.push_back(']');

    out.append(",\"sources\":[");
    for (size_t i = 0; i < source.sources.size(); ++i) {
        if (i > 0) {
            out.push_back(',');
        }
        append_source_json(out, source.sources[i]);
    }
    out.append("]}");
}

static std::string build_state_json()
{
    const std::vector<SceneNode> scenes = collect_state_tree();

    std::string out;
    out.reserve(8192);
    out.append("{\"scenes\":[");
    for (size_t i = 0; i < scenes.size(); ++i) {
        if (i > 0) {
            out.push_back(',');
        }

        out.append("{\"name\":");
        append_json_string(out, scenes[i].name);
        out.append(",\"sources\":[");

        for (size_t j = 0; j < scenes[i].sources.size(); ++j) {
            if (j > 0) {
                out.push_back(',');
            }
            append_source_json(out, scenes[i].sources[j]);
        }

        out.append("]}");
    }
    out.append("]}");

    return out;
}

static bool send_all(SOCKET socket_handle, const char *data, size_t size)
{
    while (size > 0) {
        const int chunk = size > static_cast<size_t>(INT_MAX) ? INT_MAX : static_cast<int>(size);
        const int sent = send(socket_handle, data, chunk, 0);
        if (sent <= 0) {
            return false;
        }

        data += sent;
        size -= static_cast<size_t>(sent);
    }

    return true;
}

static void send_http_response(SOCKET socket_handle, const char *status_line, const std::string &body)
{
    std::string headers;
    headers.reserve(192);
    headers.append("HTTP/1.1 ");
    headers.append(status_line);
    headers.append("\r\nContent-Type: application/json; charset=utf-8");
    headers.append("\r\nContent-Length: ");
    headers.append(std::to_string(body.size()));
    headers.append("\r\nConnection: close");
    headers.append("\r\nCache-Control: no-store");
    headers.append("\r\nAccess-Control-Allow-Origin: *");
    headers.append("\r\n\r\n");

    if (!send_all(socket_handle, headers.data(), headers.size())) {
        return;
    }

    send_all(socket_handle, body.data(), body.size());
}

enum class HttpRoute {
    State,
    NotFound,
    MethodNotAllowed,
    BadRequest,
};

static HttpRoute classify_request(const std::string &request)
{
    const size_t line_end = request.find("\r\n");
    if (line_end == std::string::npos) {
        return HttpRoute::BadRequest;
    }

    const std::string request_line = request.substr(0, line_end);
    if (request_line.rfind("GET ", 0) == 0) {
        const size_t path_start = 4;
        const size_t path_end = request_line.find(' ', path_start);
        if (path_end == std::string::npos) {
            return HttpRoute::BadRequest;
        }

        std::string path = request_line.substr(path_start, path_end - path_start);
        const size_t query_pos = path.find('?');
        if (query_pos != std::string::npos) {
            path.resize(query_pos);
        }

        if (path == "/") {
            return HttpRoute::State;
        }

        return HttpRoute::NotFound;
    }

    if (request_line.find(" HTTP/") != std::string::npos) {
        return HttpRoute::MethodNotAllowed;
    }

    return HttpRoute::BadRequest;
}

static bool read_http_request(SOCKET socket_handle, std::string &request)
{
    request.clear();
    request.reserve(2048);

    char buffer[2048];
    while (request.size() < kMaxHttpRequestSize && !g_state_reader_stop.load(std::memory_order_acquire)) {
        const int bytes_read = recv(socket_handle, buffer, sizeof(buffer), 0);
        if (bytes_read <= 0) {
            return !request.empty();
        }

        request.append(buffer, static_cast<size_t>(bytes_read));
        if (request.find("\r\n\r\n") != std::string::npos) {
            return true;
        }
    }

    return !request.empty();
}

static void close_socket_handle(SOCKET &socket_handle, bool graceful)
{
    if (socket_handle == INVALID_SOCKET) {
        return;
    }

    if (graceful) {
        shutdown(socket_handle, SD_BOTH);
    }

    closesocket(socket_handle);
    socket_handle = INVALID_SOCKET;
}

static void handle_client(SOCKET client)
{
    const DWORD recv_timeout_ms = 500;
    setsockopt(client, SOL_SOCKET, SO_RCVTIMEO,
               reinterpret_cast<const char *>(&recv_timeout_ms), sizeof(recv_timeout_ms));

    std::string request;
    if (!read_http_request(client, request)) {
        return;
    }

    switch (classify_request(request)) {
    case HttpRoute::State:
        send_http_response(client, "200 OK", build_state_json());
        break;
    case HttpRoute::NotFound:
        send_http_response(client, "404 Not Found", "{\"error\":\"not_found\"}");
        break;
    case HttpRoute::MethodNotAllowed:
        send_http_response(client, "405 Method Not Allowed", "{\"error\":\"method_not_allowed\"}");
        break;
    case HttpRoute::BadRequest:
    default:
        send_http_response(client, "400 Bad Request", "{\"error\":\"bad_request\"}");
        break;
    }
}

static void state_reader_thread_main(int port)
{
    WSADATA wsa_data{};
    if (WSAStartup(MAKEWORD(2, 2), &wsa_data) != 0) {
        blog(LOG_ERROR, "[hot-cue-mesh] state reader WSAStartup failed");
        return;
    }

    const SOCKET listener = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (listener == INVALID_SOCKET) {
        blog(LOG_ERROR, "[hot-cue-mesh] state reader socket() failed");
        WSACleanup();
        return;
    }

    {
        std::lock_guard<std::mutex> lock(g_state_reader_socket_mutex);
        g_state_reader_listener = listener;
    }

    const BOOL reuse_addr = TRUE;
    setsockopt(listener, SOL_SOCKET, SO_REUSEADDR,
               reinterpret_cast<const char *>(&reuse_addr), sizeof(reuse_addr));

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_port = htons(static_cast<u_short>(port));
    addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);

    if (bind(listener, reinterpret_cast<const sockaddr *>(&addr), sizeof(addr)) == SOCKET_ERROR) {
        blog(LOG_ERROR, "[hot-cue-mesh] state reader bind() failed on 127.0.0.1:%d", port);
        {
            std::lock_guard<std::mutex> lock(g_state_reader_socket_mutex);
            close_socket_handle(g_state_reader_listener, false);
        }
        WSACleanup();
        return;
    }

    if (listen(listener, SOMAXCONN) == SOCKET_ERROR) {
        blog(LOG_ERROR, "[hot-cue-mesh] state reader listen() failed");
        {
            std::lock_guard<std::mutex> lock(g_state_reader_socket_mutex);
            close_socket_handle(g_state_reader_listener, false);
        }
        WSACleanup();
        return;
    }

    blog(LOG_INFO, "[hot-cue-mesh] state reader listening on http://127.0.0.1:%d/", port);

    while (!g_state_reader_stop.load(std::memory_order_acquire)) {
        const SOCKET client = accept(listener, nullptr, nullptr);
        if (client == INVALID_SOCKET) {
            if (g_state_reader_stop.load(std::memory_order_acquire)) {
                break;
            }
            continue;
        }

        {
            std::lock_guard<std::mutex> lock(g_state_reader_socket_mutex);
            g_state_reader_client = client;
        }

        handle_client(client);

        {
            std::lock_guard<std::mutex> lock(g_state_reader_socket_mutex);
            if (g_state_reader_client == client) {
                close_socket_handle(g_state_reader_client, true);
            }
        }
    }

    {
        std::lock_guard<std::mutex> lock(g_state_reader_socket_mutex);
        close_socket_handle(g_state_reader_client, true);
        close_socket_handle(g_state_reader_listener, false);
    }

    WSACleanup();
}
#endif
} // namespace

void start_state_reader_server(int port)
{
#ifdef _WIN32
    if (port <= 0 || port > 65535) {
        blog(LOG_WARNING, "[hot-cue-mesh] invalid state reader port %d, using 7779", port);
        port = 7779;
    }

    stop_state_reader_server();
    g_state_reader_stop.store(false, std::memory_order_release);
    g_state_reader_thread = std::thread([port]() { state_reader_thread_main(port); });
#else
    (void)port;
    blog(LOG_WARNING, "[hot-cue-mesh] state reader server is only implemented for Windows builds");
#endif
}

void stop_state_reader_server()
{
#ifdef _WIN32
    g_state_reader_stop.store(true, std::memory_order_release);

    {
        std::lock_guard<std::mutex> lock(g_state_reader_socket_mutex);
        close_socket_handle(g_state_reader_client, true);
        close_socket_handle(g_state_reader_listener, false);
    }

    if (g_state_reader_thread.joinable()) {
        g_state_reader_thread.join();
    }
#endif
}
