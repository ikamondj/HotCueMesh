#include "StateReader.hpp"
#include <obs-module.h>
//TODO implement start and stop servers. the server should have a single get endpoint that returns all scene names, all source names in the scenes including nested sources like group1->vid1 being different than vid1, and all filters on each source. returns as a json tree
