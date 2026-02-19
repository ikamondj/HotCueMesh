package main

import (
	"encoding/json"
	"errors"
	"io"
	"log"
	"net/http"
)

const (
	configUpdaterPath      = "/app-state"
	maxConfigPayloadBytes  = 1 << 22 // 4MB
	defaultConfigServerAdr = ":8080"
)

func StartConfigUpdaterServer(appStateUpdates chan<- []Trigger, port string) *http.Server {
	return StartConfigUpdaterServerOnAddr(":"+port, appStateUpdates)
}

func StartConfigUpdaterServerOnAddr(addr string, appStateUpdates chan<- []Trigger) *http.Server {
	mux := http.NewServeMux()
	mux.HandleFunc(configUpdaterPath, makeAppStateHandler(appStateUpdates))

	server := &http.Server{
		Addr:    addr,
		Handler: mux,
	}

	go func() {
		if err := server.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			log.Printf("config updater server error: %v", err)
		}
	}()

	return server
}

func makeAppStateHandler(appStateUpdates chan<- []Trigger) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			w.Header().Set("Allow", http.MethodPost)
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}

		defer r.Body.Close()

		decoder := json.NewDecoder(http.MaxBytesReader(w, r.Body, maxConfigPayloadBytes))
		var triggers []Trigger
		if err := decoder.Decode(&triggers); err != nil {
			http.Error(w, "invalid json payload", http.StatusBadRequest)
			return
		}

		if err := decoder.Decode(&struct{}{}); err != io.EOF {
			http.Error(w, "payload must be a single json value", http.StatusBadRequest)
			return
		}

		linkOwningTriggers(triggers)

		select {
		case appStateUpdates <- triggers:
		case <-r.Context().Done():
			return
		}

		w.WriteHeader(http.StatusAccepted)
	}
}

func linkOwningTriggers(triggers []Trigger) {
	for i := range triggers {
		for j := range triggers[i].Actions {
			triggers[i].Actions[j].OwningTrigger = &triggers[i]
		}
	}
}
