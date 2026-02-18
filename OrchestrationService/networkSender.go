package main

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net"
	"net/http"
	"strings"
	"time"

	"github.com/hypebeast/go-osc/osc"
)

type ActionList struct {
	Actions []TriggerAction `json:"actions"`
}

func standardNetPayload(action TriggerAction) ([]byte, error) {
	trig := action.OwningTrigger
	return json.Marshal(trig)
}

type Protocol string

const (
	ProtoHTTP Protocol = "http"
	ProtoUDP  Protocol = "udp"
	ProtoTCP  Protocol = "tcp"
	ProtoOSC  Protocol = "osc"
	ProtoZMQ  Protocol = "zmq"
)

type AppTarget struct {
	Proto Protocol

	HTTP struct {
		URL     string
		Method  string
		Headers map[string]string
		Timeout time.Duration
	}

	UDP struct {
		Addr string
	}

	TCP struct {
		Addr    string
		Timeout time.Duration
		// If you need framing, add LengthPrefix bool or NewlineTerminated bool, etc.
	}

	OSC struct {
		Addr    string
		Path    string
		Timeout time.Duration
	}

	ZMQ struct {
		Mode    string // "push" or "pub" (extend if needed)
		Addr    string
		Timeout time.Duration
		Topic   string // used for PUB, optional
	}
}

type Router struct {
	Targets map[AppID]AppTarget // key is AppId
	Client  *http.Client
}

func NewRouter(targets map[AppID]AppTarget) *Router {
	return &Router{
		Targets: targets,
		Client:  &http.Client{},
	}
}

func (r *Router) SendActions(ctx context.Context, actions []TriggerAction, appId AppID) error {
	tgt, ok := r.Targets[appId]
	if !ok {
		return fmt.Errorf("no target configured for appId=%q", appId)
	}

	//assert all actions have the same AppId as appId
	for _, action := range actions {
		if action.AppId != appId {
			return fmt.Errorf("action appId %q does not match target appId %q", action.AppId, appId)
		}
	}

	payload, err := standardNetPayload(actions)
	if err != nil {
		return err
	}

	switch tgt.Proto {
	case ProtoHTTP:
		return sendHTTP(ctx, r.Client, tgt, payload)
	case ProtoUDP:
		return sendUDP(ctx, tgt, payload)
	case ProtoTCP:
		return sendTCP(ctx, tgt, payload)
	case ProtoOSC:
		return sendOSC(ctx, tgt, payload)
	default:
		return fmt.Errorf("unsupported protocol %q for appId=%q", tgt.Proto, appId)
	}
}

func sendHTTP(ctx context.Context, client *http.Client, tgt AppTarget, payload []byte) error {
	method := tgt.HTTP.Method
	if method == "" {
		method = http.MethodPost
	}
	if tgt.HTTP.URL == "" {
		return errors.New("http url is required")
	}

	timeout := tgt.HTTP.Timeout
	if timeout <= 0 {
		timeout = 5 * time.Second
	}

	cctx, cancel := context.WithTimeout(ctx, timeout)
	defer cancel()

	req, err := http.NewRequestWithContext(cctx, method, tgt.HTTP.URL, bytes.NewReader(payload))
	if err != nil {
		return err
	}
	req.Header.Set("Content-Type", "application/json")
	for k, v := range tgt.HTTP.Headers {
		req.Header.Set(k, v)
	}

	resp, err := client.Do(req)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		b, _ := io.ReadAll(io.LimitReader(resp.Body, 4096))
		return fmt.Errorf("http %s: %s: %s", method, resp.Status, strings.TrimSpace(string(b)))
	}

	return nil
}

func sendUDP(ctx context.Context, tgt AppTarget, payload []byte) error {
	if tgt.UDP.Addr == "" {
		return errors.New("udp addr is required")
	}

	d := net.Dialer{}
	conn, err := d.DialContext(ctx, "udp", tgt.UDP.Addr)
	if err != nil {
		return err
	}
	defer conn.Close()

	_, err = conn.Write(payload)
	return err
}

func sendTCP(ctx context.Context, tgt AppTarget, payload []byte) error {
	if tgt.TCP.Addr == "" {
		return errors.New("tcp addr is required")
	}

	timeout := tgt.TCP.Timeout
	if timeout <= 0 {
		timeout = 5 * time.Second
	}
	cctx, cancel := context.WithTimeout(ctx, timeout)
	defer cancel()

	d := net.Dialer{}
	conn, err := d.DialContext(cctx, "tcp", tgt.TCP.Addr)
	if err != nil {
		return err
	}
	defer conn.Close()

	_ = conn.SetWriteDeadline(time.Now().Add(timeout))
	_, err = conn.Write(payload)
	return err
}

func sendOSC(ctx context.Context, tgt AppTarget, payload []byte) error {
	if tgt.OSC.Addr == "" {
		return errors.New("osc addr is required")
	}
	path := tgt.OSC.Path
	if path == "" {
		path = "/trigger"
	}

	timeout := tgt.OSC.Timeout
	if timeout <= 0 {
		timeout = 3 * time.Second
	}

	host, portStr, err := net.SplitHostPort(tgt.OSC.Addr)
	if err != nil {
		return fmt.Errorf("osc addr must be host:port: %w", err)
	}

	client := osc.NewClient(host, mustAtoi(portStr))
	msg := osc.NewMessage(path)
	msg.Append(string(payload))

	done := make(chan error, 1)
	go func() { done <- client.Send(msg) }()

	select {
	case <-ctx.Done():
		return ctx.Err()
	case err := <-done:
		return err
	case <-time.After(timeout):
		return fmt.Errorf("osc send timeout after %s", timeout)
	}
}

func mustAtoi(s string) int {
	var n int
	for _, r := range s {
		if r < '0' || r > '9' {
			break
		}
		n = n*10 + int(r-'0')
	}
	return n
}
