package com.ikamon.hotCueMesh.persistenceService.service;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.web.client.RestTemplate;

import com.ikamon.hotCueMesh.persistenceService.constants.CueColor;
import com.ikamon.hotCueMesh.persistenceService.constants.Decks;
import com.ikamon.hotCueMesh.persistenceService.constants.HotcueType;
import com.ikamon.hotCueMesh.persistenceService.dto.orchestrator.TriggerActionOrch;
import com.ikamon.hotCueMesh.persistenceService.dto.orchestrator.TriggerOrch;
import com.ikamon.hotCueMesh.persistenceService.entity.Action;
import com.ikamon.hotCueMesh.persistenceService.entity.Trigger;
import com.ikamon.hotCueMesh.persistenceService.repository.ActionRepository;
import com.ikamon.hotCueMesh.persistenceService.repository.TriggerRepository;

@Service
public class OrchestratorService {
	@Autowired
	private RestTemplate restTemplate;
	@Autowired
	private ActionRepository actionRepository;
	@Autowired
	private TriggerRepository triggerRepository;
	@Value("${orchestrator.url}")
	private String url;
	//TODO hit the orchestrator endpoint
	public void update() {
		List<TriggerOrch> triggers = new ArrayList<TriggerOrch>();
		List<Trigger> triggerEntities = triggerRepository.findAll();
		for (Trigger trigEntity : triggerEntities) {
			if (!trigEntity.getEnabled()) {continue;}
			TriggerOrch torch = new TriggerOrch();
			Map<String, Boolean> hotCueType = new HashMap<>();
			int hcCombined = trigEntity.getHotcueType();
			torch.setHotCueType(HotcueType.getHotcueTypeMap(hcCombined));
			torch.setCueMatchType(trigEntity.getCueMatchType().name());
			torch.setCueColor(CueColor.getCueColorMap(trigEntity.getCueColor()));
			torch.setDecks(Decks.getDeckMap(trigEntity.getDecks()));
			torch.setCueName(trigEntity.getCueName());
			List<TriggerActionOrch> acts = new ArrayList<>();
			for (Action actionEntity : trigEntity.getActions()) {
				TriggerActionOrch aorch = new TriggerActionOrch();
				aorch.setAppId(actionEntity.getAppId());
				aorch.setActionType(actionEntity.getActionType());
				aorch.setArgs(actionEntity.getActionArgsMap());
			}
		}
		restTemplate.postForEntity(url, triggers, Void.class);
	}
}
