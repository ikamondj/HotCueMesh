package main

import (
	"context"
	"time"
)

// HotcueEvent is the normalized event payload emitted by the VirtualDJ plugin.
type HotcueEvent struct {
	Source    string
	Deck      int
	HotcueID  int
	EventType string
	Timestamp time.Time
	Metadata  map[string]string
}

// ZeroMQListener consumes runtime events from ZeroMQ and exposes them to the orchestrator.
type ZeroMQListener interface {
	Listen(ctx context.Context) (<-chan HotcueEvent, <-chan error)
	Close() error
}
