package com.ikamon.hotCueMesh.persistenceService.service;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.web.client.RestTemplate;

@Service
public class OrchestratorService {
	@Autowired
	private RestTemplate restTemplate;
	@Value("${orchestrator.url}")
	private String url;
	//TODO hit the orchestrator endpoint
}
