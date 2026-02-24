#include "pch.h"
#include "UdpSender.h"
#include "vdjPlugin8.h"
#include "json.hpp"
#include <queue>
#include <thread>
#include <functional>
#include <tuple>
#include <set>
#include <map>
#include <string>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <chrono>
#include <atomic>
#include <sstream>
#include <algorithm>
#include <cctype>
#include <cstdlib>

#pragma comment(lib, "ws2_32.lib")

using json = nlohmann::json;

namespace {
constexpr const char* kEventHost = "127.0.0.1";
constexpr unsigned short kEventPort = 8112;
constexpr unsigned short kResetPort = 5029;
}

std::map<int, std::string> deckSongTitle;
std::map<int, std::queue<std::pair<int, double>>> deckCueQueue;
extern std::set<std::pair<std::string, std::string>> alreadySent;

std::string UDPTrackInfoSender::GetInfoText(const std::string& command) {
    char output[1024] = { 0 };
    HRESULT result = GetStringInfo(command.c_str(), output, sizeof(output));
    return (result == S_OK) ? std::string(output) : "";
}

double UDPTrackInfoSender::GetInfoDouble(const std::string& command) {
    double d = -1.0;
    HRESULT result = GetInfo(command.c_str(), &d);
    return (result == S_OK) ? d : -1.0;
}

void UDPTrackInfoSender::SendJsonMessage(const std::string& jsonStr) {
    if (!USE_TCP) {
        return;
    }

    SOCKET sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (sock == INVALID_SOCKET) {
        return;
    }

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_port = htons(kEventPort);
    inet_pton(AF_INET, kEventHost, &addr.sin_addr);

    if (connect(sock, (sockaddr*)&addr, sizeof(addr)) == 0) {
        const char* buffer = jsonStr.c_str();
        int remaining = static_cast<int>(jsonStr.size());
        while (remaining > 0) {
            int written = send(sock, buffer, remaining, 0);
            if (written <= 0) {
                break;
            }
            buffer += written;
            remaining -= written;
        }
    }

    closesocket(sock);
}

void UDPTrackInfoSender::PollStateChanges() {
    auto toLower = [](std::string value) {
        std::transform(value.begin(), value.end(), value.begin(), [](unsigned char c) {
            return static_cast<char>(std::tolower(c));
            });
        return value;
        };

    auto deckToMask = [](int deck) -> int {
        return (deck >= 1 && deck <= 4) ? (1 << (deck - 1)) : 0;
        };

    auto normalizeHotCueType = [&toLower](const std::string& rawType) -> std::string {
        std::string normalized = toLower(rawType);

        if (normalized == "hot_cue" || normalized == "hot cue" || normalized == "1") return "Hot_Cue";
        if (normalized == "saved_loop" || normalized == "saved loop" || normalized == "2") return "Saved_Loop";
        if (normalized == "action" || normalized == "4") return "Action";
        if (normalized == "remix_point" || normalized == "remix point" || normalized == "8") return "Remix_Point";
        if (normalized == "beatgrid_anchor" || normalized == "beatgrid anchor" || normalized == "16") return "BeatGrid_Anchor";
        if (normalized == "automix_point" || normalized == "automix point" || normalized == "32") return "Automix_Point";
        if (normalized == "load_point" || normalized == "load point" || normalized == "64") return "Load_Point";

        return "Hot_Cue";
        };

    auto normalizeCueColor = [](const std::string& rawColor) -> uint16_t {
        if (rawColor.empty()) {
            return 8; // White fallback
        }

        char* end = nullptr;
        unsigned long parsed = strtoul(rawColor.c_str(), &end, 0);
        if (end == rawColor.c_str()) {
            return 8; // White fallback
        }

        uint16_t color = static_cast<uint16_t>(parsed);
        if (color != 0 && (color & (color - 1)) == 0) {
            return color;
        }

        for (uint16_t bit = 1; bit != 0; bit <<= 1) {
            if ((color & bit) != 0) {
                return bit;
            }
        }

        return 8; // White fallback
        };

    while (running.load()) {
        for (int deck = 1; deck <= 4; ++deck) {

            std::string audible = toLower(GetInfoText("deck " + std::to_string(deck) + " is_audible"));
            if (audible != "on" && audible != "yes" && audible != "true") continue;
            std::string title = GetInfoText("deck " + std::to_string(deck) + " get_title");
            if (title.empty()) continue;

            double cursorPercent = GetInfoDouble("deck " + std::to_string(deck) + " get_position");
            if (cursorPercent < 0.0) continue;

            for (int cue = 1; cue <= 128; ++cue) {
                std::string hasCue = GetInfoText("deck " + std::to_string(deck) + " has_cue " + std::to_string(cue));
                hasCue = toLower(hasCue);
                if (hasCue != "on") continue;
                std::string cueName = GetInfoText("deck " + std::to_string(deck) + " cue_name " + std::to_string(cue));
                double cuePercent = GetInfoDouble("deck " + std::to_string(deck) + " cue_pos " + std::to_string(cue));
                if (cuePercent < 0.0) continue;

                auto key = std::make_pair(title, std::to_string(cue));
                if (cursorPercent < cuePercent) {
                    if (alreadySent.find(key) != alreadySent.end()) {
                        alreadySent.erase(key);
                    }
                }
                else {

                    if (alreadySent.find(key) == alreadySent.end()) {
                        std::string cueTypeRaw = GetInfoText("deck " + std::to_string(deck) + " cue_type " + std::to_string(cue));
                        std::string cueColorRaw = GetInfoText("deck " + std::to_string(deck) + " cue_color " + std::to_string(cue));
			std::string cueMeta = GetInfoText("deck " + std::to_string(deck) + " cue_display " + std::to_string(cue));
			//std::string cueOption = GetInfoText("deck " + std::to_string(deck) + " cues_options " + std);
                        json event = {
                            {"CueMatchType", "None"},
                            {"CueName", cueName},
                            {"CueColor", cueColorRaw},
                            {"Deck", deckToMask(deck)},
                            {"HotCueType", normalizeHotCueType(cueTypeRaw)},
			    {"meta", cueMeta}
                        };

                        SendJsonMessage(event.dump());

                        alreadySent.insert(key);
                    }
                }
            }
        }

        const int chunkMs = 10;
        int remaining = frequencyMs;
        while (running.load() && remaining > 0) {
            std::this_thread::sleep_for(std::chrono::milliseconds(min(chunkMs, remaining)));
            remaining -= chunkMs;
        }

    }
}

void UDPTrackInfoSender::StartResetListener() {
    SOCKET listenSock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (listenSock == INVALID_SOCKET) return;

    sockaddr_in serverAddr{};
    serverAddr.sin_family = AF_INET;
    serverAddr.sin_port = htons(kResetPort);
    serverAddr.sin_addr.s_addr = INADDR_ANY;

    if (bind(listenSock, (sockaddr*)&serverAddr, sizeof(serverAddr)) == SOCKET_ERROR) {
        closesocket(listenSock);
        return;
    }

    listen(listenSock, 1);

    while (running.load()) {
        fd_set readfds;
        FD_ZERO(&readfds);
        FD_SET(listenSock, &readfds);

        timeval tv{};
        tv.tv_sec = 0;
        tv.tv_usec = 100000; // 100ms timeout

        int activity = select(0, &readfds, NULL, NULL, &tv);
        if (!running.load()) break;

        if (activity > 0 && FD_ISSET(listenSock, &readfds)) {
            SOCKET clientSock = accept(listenSock, NULL, NULL);
            if (clientSock != INVALID_SOCKET) {
                char buffer[256] = {};
                int received = recv(clientSock, buffer, sizeof(buffer) - 1, 0);
                if (received > 0) {
                    std::string msg(buffer, received);
                    if (msg.find("reset") != std::string::npos) {
                        alreadySent.clear();
                    }
                }
                closesocket(clientSock);
            }
        }
    }

    closesocket(listenSock);
}

HRESULT VDJ_API UDPTrackInfoSender::OnLoad() {
    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        return E_FAIL;
    }
    USE_NAMED_PIPE = false;
    std::this_thread::sleep_for(std::chrono::milliseconds(10));
    running.store(true);
    senderThread = std::thread(&UDPTrackInfoSender::PollStateChanges, this);
    resetListenerThread = std::thread(&UDPTrackInfoSender::StartResetListener, this);

    return S_OK;
}

HRESULT VDJ_API UDPTrackInfoSender::OnGetPluginInfo(TVdjPluginInfo8* infos) {
    infos->PluginName = "TCP Event Sender";
    infos->Author = "Ikamon";
    infos->Description = "Sends VDJ cue/master events as JSON over TCP";
    infos->Version = "1.0";
    infos->Flags = 0x00;
    infos->Bitmap = NULL;
    return S_OK;
}

ULONG VDJ_API UDPTrackInfoSender::Release() {
    running.store(false);

    if (senderThread.joinable()) senderThread.join();
    if (resetListenerThread.joinable()) resetListenerThread.join();

    WSACleanup();
    return 0;
}

HRESULT VDJ_API UDPTrackInfoSender::OnGetUserInterface(TVdjPluginInterface8* pluginInterface) {
    return S_OK;
}
HRESULT VDJ_API UDPTrackInfoSender::OnParameter(int id) {
    return S_OK;
}
HRESULT VDJ_API UDPTrackInfoSender::OnGetParameterString(int id, char* outParam, int outParamSize) {
    return S_OK;
}
