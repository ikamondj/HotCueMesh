package com.ikamon.hotCueMesh.persistenceService.entity;

import java.util.List;

import com.ikamon.hotCueMesh.persistenceService.constants.CueMatch;

import jakarta.persistence.CascadeType;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.OneToMany;
import jakarta.persistence.Table;
import lombok.Builder;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
@Entity
@Builder
@Table(name = "TRIGGER")
public class Trigger {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private long triggerId;

    @Column(nullable = false)
    private int hotcueType;

    @Column(nullable = false)
    private int cueColor; // <- was int

    @Column(nullable = true)
    private String cueName;

    @Column(nullable = false)
    private Boolean enabled;

    @Column(nullable = false)
    private int decks;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private CueMatch cueMatchType;

    @OneToMany(mappedBy = "trigger", cascade = CascadeType.ALL, orphanRemoval = true)
    private List<Action> actions;

    public void setActions(List<Action> actions) {
	this.actions = actions;
	if (actions != null) {
	    for (Action a : actions) {
		a.setTrigger(this);
	    }
	}
    }
}
