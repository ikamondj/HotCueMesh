package main

import (
	"fmt"
)

func main() {
	fmt.Println("Orchestration Service is running...")
	appstateChannel := make(chan []Trigger, 10)
	go StartConfigUpdaterServer(appstateChannel, "8111")

	eventsReceived := make(chan HotcueEvent, 0)
	//TODO implement StartTCPEventListener in listener.go that listens for TCP HotCueEvents and pushes them onto a channel
	go StartTCPEventListener(eventsReceived, "8112")

	appstate := make([]Trigger, 0, 1024)

	for {
		trigs, ok := <-appstateChannel
		if ok {
			clear(appstate)
			appstate = trigs
		}

		event, ok := <-eventsReceived
		if ok {
			ProcessHotcueEvent(event, appstate)
		}

	}

}
