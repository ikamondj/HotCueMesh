package com.ikamon.hotCueMesh.persistenceService.controller;

import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestMethod;
import org.springframework.web.bind.annotation.RequestParam;


@Controller
public class MappingController {

    @RequestMapping(name="add", method=RequestMethod.POST)
    public String requestMethodName(@RequestParam String param) {
        return new String();
    }
    
}
