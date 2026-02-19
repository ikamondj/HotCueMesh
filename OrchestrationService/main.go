package main

import (
	"fmt"
)

func main() {
	fmt.Println("Orchestration Service is running...")
	appstateChannel := make(chan []Trigger, 10)
	go StartConfigUpdaterServer(appstateChannel, "8111")

	eventsReceived := make(chan HotcueEvent, 0)
	go StartTCPEventListener(eventsReceived, "8112")

	appstate := make([]Trigger, 0, 1024)

	for {
		select {
		case trigs, ok := <-appstateChannel:
			if ok {
				clear(appstate)
				appstate = trigs
			}
		}
		select {
		case event, ok := <-eventsReceived:
			if ok {
				processHotcueEvent(event, appstate)
			}
		}
	}

}
