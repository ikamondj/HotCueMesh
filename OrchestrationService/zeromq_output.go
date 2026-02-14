package main

import "context"

// ZeroMQOutput publishes matched trigger actions to downstream services via ZeroMQ.
type ZeroMQOutput interface {
	PublishAction(ctx context.Context, action OutputAction, event HotcueEvent) error
	Close() error
}
