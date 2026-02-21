package com.ikamon.hotCueMesh.persistenceService.entity;

import java.util.HashMap;
import java.util.Map;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectReader;
import com.fasterxml.jackson.databind.json.JsonMapper;
import com.ikamon.hotCueMesh.persistenceService.constants.CueMatch;
import com.ikamon.hotCueMesh.persistenceService.dto.ActionDto;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.FetchType;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import lombok.Getter;
import lombok.Setter;


@Getter
@Setter
@Entity(name="ACTION")
public class Action {
    private static final ObjectReader ACTION_ARGS_READER =
        JsonMapper.builder().build().readerFor(new TypeReference<HashMap<String, String>>() {});
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private long actionId;
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "triggerId", nullable = false)
    private Trigger trigger;
    @Column(nullable = false)
    private String appId;
    @Column(nullable = false)
    private String actionType;
    @Column(nullable = false)
    private String actionArgs;
    public Map<String, String> getActionArgsMap() {
	if (actionArgs == null || actionArgs.isBlank()) {
	    return new HashMap<>();
	}
	try {
	    return ACTION_ARGS_READER.readValue(actionArgs);
	} catch (Exception ignored) {
	    return new HashMap<>();
	}
    }

    public ActionDto toDto() {
	ActionDto dto = new ActionDto();
	dto.setActionArgs(actionArgs);
	dto.setActionType(actionType);
	dto.setAppId(appId);
	return dto;
    }
}
