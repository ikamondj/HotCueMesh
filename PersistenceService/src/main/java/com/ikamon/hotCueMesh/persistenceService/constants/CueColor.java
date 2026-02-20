package com.ikamon.hotCueMesh.persistenceService.constants;

import java.util.HashMap;
import java.util.Map;

public class CueColor {
    public static int Invisible = 0x1;
    public static int DarkGrey = 0x2;
    public static int LightGrey = 0x4;
    public static int White = 0x8;
    public static int Burgundy = 0x10;
    public static int Apricot = 0x20;
    public static int Red = 0x40;
    public static int Orange = 0x80;
    public static int Yellow = 0x100;
    public static int Eggshell = 0x200;
    public static int Green = 0x400;
    public static int Cyan = 0x800;
    public static int Cobalt = 0x1000;
    public static int Blue = 0x2000;
    public static int Purple = 0x4000;
    public static int Magenta = 0x8000;
    public static int all = 0xFFFF;
    public static Map<Integer, Boolean> getCueColorMap(int cueColor) {
	Map<Integer, Boolean> result = new HashMap<>();
	for (int col = Invisible; col < Magenta + 1; col <<= 1) {
		if ((cueColor & col) != 0) {
			result.put(col, true);
		}
	}
	return result;
    }
}
