package com.ikamon.hotCueMesh.persistenceService.service;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.web.client.RestClientException;
import org.springframework.web.client.RestTemplate;

import com.ikamon.hotCueMesh.persistenceService.dto.ObsState;

@Service
public class ObsStateService {
	private static final int MAX_RETRIES = 3;
	private static final long RETRY_DELAY_MS = 100;

	@Value("${obs.receiver.url}")
	private String obsReceiverUrl;
	@Autowired
	private RestTemplate restTemplate;

	public ObsState getObsState() {
		RestClientException lastException = null;

		for (int attempt = 1; attempt <= MAX_RETRIES; attempt++) {
			try {
				return restTemplate.getForObject(obsReceiverUrl, ObsState.class);
			} catch (RestClientException ex) {
				lastException = ex;
				if (attempt == MAX_RETRIES) {
					break;
				}
				try {
					Thread.sleep(RETRY_DELAY_MS);
				} catch (InterruptedException ie) {
					Thread.currentThread().interrupt();
					throw new IllegalStateException("Interrupted while retrying OBS state request", ie);
				}
			}
		}

		throw new IllegalStateException("Failed to fetch OBS state after " + MAX_RETRIES + " attempts", lastException);
	}
}
