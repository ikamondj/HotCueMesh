package main

import (
	"fmt"
	"strings"
)

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

		//execute actions for this trigger
		for _, action := range trigger.Actions {
			destinationApp := action.AppId
			//send action to appToSend
			fmt.Printf("Would send action %s to app %s with args %v\n", action.ActionType, destinationApp, action.Args)
		}
	}
}
