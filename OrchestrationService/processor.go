package main

import (
	"context"
	"fmt"
	"strings"
)

var networkAppIDs = map[AppID]bool{
	OBS:                false,
	LightingController: false,
	HttpEndpoint:       true,
	TcpEndpoint:        true,
	UdpEndpoint:        true,
	OscEndpoint:        true,
}

var router = Router{} //TODO initialize router properly

func skipEvent(event HotcueEvent, trigger Trigger) bool {
	return !trigger.HotCueType[event.HotCueType] ||
		!trigger.CueColor[event.CueColor] ||
		!trigger.Decks[event.Deck] ||
		(trigger.CueMatchType == Exact && event.CueName != trigger.CueName) ||
		(trigger.CueMatchType == Contains && !strings.Contains(event.CueName, trigger.CueName)) ||
		(trigger.CueMatchType == StartsWith && !strings.HasPrefix(event.CueName, trigger.CueName)) ||
		(trigger.CueMatchType == EndsWith && !strings.HasSuffix(event.CueName, trigger.CueName)) ||
		(trigger.CueMatchType == Embedded && !strings.Contains(event.CueName, trigger.CueName))
}

func processHotcueEvent(event HotcueEvent, mappings []Trigger) {
	for _, trigger := range mappings {
		if skipEvent(event, trigger) {
			continue
		}

		netActions := []TriggerAction{}

		//execute actions for this trigger
		for _, action := range trigger.Actions {
			destinationApp := action.AppId
			if networkAppIDs[destinationApp] {
				netActions = append(netActions, action)
				continue
			}
			//send action to appToSend
			fmt.Printf("Would send action %s to app %s with args %v\n", action.ActionType, destinationApp, action.Args)
		}

		if len(netActions) > 0 {
			dest := netActions[0].AppId
			err := router.SendActions(context.Background(), netActions, dest)
			if err != nil {
				fmt.Printf("Error sending actions to %s: %v\n", dest, err)
			}
		}
	}
}
