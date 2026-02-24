#ifndef MYPLUGIN8_H
#define MYPLUGIN8_H

#include <stdio.h>

#include "vdjDsp8.h"
#include <string>
#include <iostream>
#include <string>
#include <thread>
#include <ws2tcpip.h>
#include <chrono>
#include <vector>
#include <array>
#include <winsock2.h>
#include <queue>
#include <map>
#include <set>


class UDPTrackInfoSender : public IVdjPlugin8
{
private:
	bool USE_TCP = true, USE_NAMED_PIPE = false;
	std::atomic<bool> running;
	int frequencyMs = 35;
	std::thread senderThread;
	SOCKET udpSocket;
	sockaddr_in serverAddr;
	std::string GetInfoText(const std::basic_string<char>& command);
	double GetInfoDouble(const std::basic_string<char>& command);
	void SendTrackInfo();
public:

	HRESULT VDJ_API OnLoad();
	HRESULT VDJ_API OnGetPluginInfo(TVdjPluginInfo8* infos);
	ULONG VDJ_API Release();
	HRESULT VDJ_API OnGetUserInterface(TVdjPluginInterface8* pluginInterface) override;
	HRESULT VDJ_API OnParameter(int id) override;
	HRESULT VDJ_API OnGetParameterString(int id, char* outParam, int outParamSize) override;

private:
	int m_Reset;
	float m_Dry;
	float m_Wet;

	bool isMasterFX(); // an example of additional function for the use of GetInfo()

protected:
	std::map<int, std::string> deckSongTitle;
	std::map<int, std::queue<std::pair<int, double>>> deckCueQueue;
	std::set<std::pair<std::string, std::string>> alreadySent;
	std::thread resetListenerThread;
	void PollStateChanges();
	void StartResetListener();
	void SendJsonMessage(const std::string& jsonStr);

	typedef enum _ID_Interface
	{
		ID_BUTTON_1,
		ID_SLIDER_1
	} ID_Interface;
};

#endif
