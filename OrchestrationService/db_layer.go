package main

import "context"

type TriggerDefinition struct {
	ID        string
	EventType string
	Deck      int
	HotcueID  int
}

type OutputAction struct {
	ID       string
	Name     string
	Protocol string
	Target   string
	Topic    string
	Payload  []byte
}

type TriggerActionMapping struct {
	Trigger TriggerDefinition
	Actions []OutputAction
}

// DBLayer is the persistence boundary used by the orchestration runtime.
type DBLayer interface {
	Ping(ctx context.Context) error
	LoadTriggerActionMappings(ctx context.Context) ([]TriggerActionMapping, error)
}
