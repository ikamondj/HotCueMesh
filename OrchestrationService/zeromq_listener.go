package main

import (
	"context"
)

// ZeroMQListener consumes runtime events from ZeroMQ and exposes them to the orchestrator.
type ZeroMQListener interface {
	Listen(ctx context.Context) (<-chan HotcueEvent, <-chan error)
	Close() error
}
