package com.ikamon.hotCueMesh.persistenceService.dto;

import com.ikamon.hotCueMesh.persistenceService.entity.Action;

import lombok.Data;

@Data
public class ActionDto {
	private String appId;
	private String actionType;
	private String actionArgs;
	public Action toEntity() {
		Action action = new Action();
		action.setAppId(appId);
		action.setActionType(actionType);
		action.setActionArgs(actionArgs);
		return action;
	}
}
