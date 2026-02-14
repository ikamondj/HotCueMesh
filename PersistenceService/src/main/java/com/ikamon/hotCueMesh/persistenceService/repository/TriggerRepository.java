package com.ikamon.hotCueMesh.persistenceService.repository;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import com.ikamon.hotCueMesh.persistenceService.entity.Trigger;

public interface TriggerRepository extends JpaRepository<Trigger, Long> {
    @Query("select t from Trigger t where t.cueName = :cueName and t.cueColor = :cueColor and t.hotcueType = :hotcueType and t.cueMatchType = :cueMatchType")
    Trigger findByCueNameAndCueColorAndHotcueTypeAndCueMatchType(@Param("cueName") String cueName, @Param("cueColor") Integer cueColor, @Param("hotcueType") String hotcueType, @Param("cueMatchType") String cueMatchType);
}
