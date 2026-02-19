package main

import (
	"bufio"
	"context"
	"encoding/json"
	"errors"
	"io"
	"log"
	"net"
)

// ZeroMQListener consumes runtime events from ZeroMQ and exposes them to the orchestrator.
type ZeroMQListener interface {
	Listen(ctx context.Context) (<-chan HotcueEvent, <-chan error)
	Close() error
}

func StartTCPEventListener(eventsReceived chan<- HotcueEvent, port string) {
	listener, err := net.Listen("tcp", ":"+port)
	if err != nil {
		log.Printf("tcp hotcue listener failed to start on :%s: %v", port, err)
		return
	}
	defer listener.Close()

	for {
		conn, err := listener.Accept()
		if err != nil {
			if netErr, ok := err.(net.Error); ok && netErr.Temporary() {
				continue
			}
			log.Printf("tcp hotcue listener accept error: %v", err)
			return
		}

		go consumeHotcueEvents(conn, eventsReceived)
	}
}

func consumeHotcueEvents(conn net.Conn, eventsReceived chan<- HotcueEvent) {
	defer conn.Close()

	decoder := json.NewDecoder(bufio.NewReaderSize(conn, 16*1024))
	for {
		var event HotcueEvent
		if err := decoder.Decode(&event); err != nil {
			if errors.Is(err, io.EOF) {
				return
			}
			log.Printf("tcp hotcue listener decode error from %s: %v", conn.RemoteAddr(), err)
			return
		}

		eventsReceived <- event
	}
}
