package com.ikamon.hotCueMesh.persistenceService.service;

import java.util.List;
import java.util.Map;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;
import org.springframework.web.client.RestTemplate;

import com.ikamon.hotCueMesh.persistenceService.entity.Action;
import com.ikamon.hotCueMesh.persistenceService.entity.Trigger;
import com.ikamon.hotCueMesh.persistenceService.repository.ActionRepository;
import com.ikamon.hotCueMesh.persistenceService.repository.TriggerRepository;

@Service
public class DataService {
	@Autowired
	private ActionRepository actionRepository;
	@Autowired
	private TriggerRepository triggerRepository;


	public String buildJsonFromDb() {
		//TODO build json that sends to orchestrator. use expected json mappings in the orchestrator app.
		triggerRepository.findAll();

		return "{}";
	}


}
