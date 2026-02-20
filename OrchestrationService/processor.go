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

func ProcessHotcueEvent(event HotcueEvent, mappings []Trigger) {
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
			appSet := make(map[AppID]TriggerAction)
			for _, netAction := range netActions {
				appSet[netAction.AppId] = netAction
			}
			for dest, netAction := range appSet {
				err := router.SendActions(context.Background(), netAction, dest)
				if err != nil {
					fmt.Printf("Error sending actions to %s: %v\n", dest, err)
				}
			}
		}
	}
}
