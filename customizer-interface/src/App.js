import React, { useState } from 'react';
import './App.css';
import logo from './logo.svg';
import TriggerItem from './components/TriggerItem';
import ActionItem from './components/ActionItem';
import { postJSON } from './api';
import {config} from './config';

const HOTCUE_TYPES = [
  'Hot_Cue',
  'Saved_Loop',
  'Action',
  'Remix_Point',
  'BeatGrid_Anchor',
  'Automix_Point',
  'Load_Point'
];

const CUE_MATCH_TYPES = ['Exact','Contains', 'StartsWith', 'EndsWith', 'Embedded'];

const CUE_COLORS = [
  { label: 'Invisible', value: 0x1, hex: 'transparent' },
  { label: 'DarkGrey', value: 0x2, hex: '#3f3f46' },
  { label: 'LightGrey', value: 0x4, hex: '#9ca3af' },
  { label: 'White', value: 0x8, hex: '#ffffff' },
  { label: 'Burgundy', value: 0x10, hex: '#800020' },
  { label: 'Apricot', value: 0x20, hex: '#f0ae86' },
  { label: 'Red', value: 0x40, hex: '#ef4444' },
  { label: 'Orange', value: 0x80, hex: '#f97316' },
  { label: 'Yellow', value: 0x100, hex: '#facc15' },
  { label: 'Eggshell', value: 0x200, hex: '#ffe9a1' },
  { label: 'Green', value: 0x400, hex: '#22c55e' },
  { label: 'Cyan', value: 0x800, hex: '#06b6d4' },
  { label: 'Cobalt', value: 0x1000, hex: '#4c7ed3' },
  { label: 'Blue', value: 0x2000, hex: '#0000ff' },
  { label: 'Purple', value: 0x4000, hex: '#a855f7' },
  { label: 'Magenta', value: 0x8000, hex: '#d946ef' }
];

const ALL_CUE_COLORS_MASK = CUE_COLORS.reduce((accumulator, color) => accumulator | color.value, 0);

const createActionDto = (id) => ({
  id,
  appId: 'OBS',
  actionType: 'display source',
  actionArgs: '',
  sourceName: '',
  filterName: '',
  sceneName: '',
  targetHost: '',
  targetPort: '',
  httpMethod: 'POST',
  endpointPath: '/',
  payload: '',
  lightingPreset: 0,
  randomPresetList: ''
});

const createTriggerDto = (id) => ({
  id,
  name: `Trigger ${id}`,
  description: '',
  hotcueType: 'Hot_Cue',
  hotcueTypes: ['Hot_Cue'],
  cueName: '',
  cueColor: ALL_CUE_COLORS_MASK,
  enabled: false,
  cueMatchType: 'None',
  actions: []
});

const ScrollView = ({ children }) => <div className="scroll-view">{children}</div>;

const Panel = ({ title, cta, children }) => (
  <div className="panel">
    <div className="panel-header">
      <div className="panel-title">{title}</div>
      {cta}
    </div>
    {children}
  </div>
);

function sendAppState(trigger) {

}

function App() {
  const [nextTriggerId, setNextTriggerId] = useState(1);
  const [nextActionId, setNextActionId] = useState(1);
  const [triggers, setTriggers] = useState([]);
  const [selectedTriggerId, setSelectedTriggerId] = useState(null);

  const addTrigger = () => {
    setTriggers((previous) => [...previous, createTriggerDto(nextTriggerId)]);
    setNextTriggerId((previous) => previous + 1);


  };

  const updateTrigger = (id, patch) => {
    setTriggers((previous) =>
      previous.map((trigger) => (trigger.id === id ? { ...trigger, ...patch } : trigger))
    );
  };

  const removeTrigger = (id) => {
    setTriggers((previous) => previous.filter((trigger) => trigger.id !== id));
    setSelectedTriggerId((previous) => (previous === id ? null : previous));
  };

  const selectTrigger = (id) => {
    setSelectedTriggerId(id);
  };

  const addActionToTrigger = (triggerId) => {
    setSelectedTriggerId(triggerId);
    const newAction = createActionDto(nextActionId);
    setTriggers((previousTriggers) =>
      previousTriggers.map((trigger) =>
        trigger.id === triggerId ? { ...trigger, actions: [...trigger.actions, newAction] } : trigger
      )
    );
    setNextActionId((previousActionId) => previousActionId + 1);
  };

  const updateActionInTrigger = (triggerId, actionId, patch) => {
    setTriggers((previousTriggers) =>
      previousTriggers.map((trigger) =>
        trigger.id === triggerId
          ? {
              ...trigger,
              actions: trigger.actions.map((action) =>
                action.id === actionId ? { ...action, ...patch } : action
              )
            }
          : trigger
      )
    );
  };

  const removeActionFromTrigger = (triggerId, actionId) => {
    setTriggers((previousTriggers) =>
      previousTriggers.map((trigger) =>
        trigger.id === triggerId
          ? { ...trigger, actions: trigger.actions.filter((action) => action.id !== actionId) }
          : trigger
      )
    );
  };

  const selectedTrigger = triggers.find((trigger) => trigger.id === selectedTriggerId) || null;
  const selectedActions = selectedTrigger ? selectedTrigger.actions : [];

  return (
    <div className="App">
      <header className="App-header">
        <div className="panel-row">
          <Panel
            title="Triggers"
            cta={
              <button type="button" className="primary-button" onClick={addTrigger}>
                Add Trigger
              </button>
            }
          >
            <ScrollView>
              {triggers.length === 0 ? (
                <div className="empty-state">No triggers yet. Click "Add Trigger" to create one.</div>
              ) : (
                triggers.map((trigger) => (
                  <TriggerItem
                    key={trigger.id}
                    trigger={trigger}
                    hotcueTypes={HOTCUE_TYPES}
                    cueMatchTypes={CUE_MATCH_TYPES}
                    colorOptions={CUE_COLORS}
                    onChange={updateTrigger}
                    onRemove={removeTrigger}
                    onSelect={selectTrigger}
                    onAddAction={addActionToTrigger}
                    selected={trigger.id === selectedTriggerId}
                  />
                ))
              )}
            </ScrollView>
          </Panel>
          <Panel title="Actions">
            <ScrollView>
              {selectedTrigger == null ? (
                <div className="empty-state">Select a trigger to view attached actions.</div>
              ) : selectedActions.length === 0 ? (
                <div className="empty-state">Trigger {selectedTrigger.id} has no actions yet.</div>
              ) : (
                selectedActions.map((action) => (
                  <ActionItem
                    key={action.id}
                    action={action}
                    onChange={(actionId, patch) =>
                      updateActionInTrigger(selectedTrigger.id, actionId, patch)
                    }
                    onRemove={(actionId) => removeActionFromTrigger(selectedTrigger.id, actionId)}
                  />
                ))
              )}
            </ScrollView>
          </Panel>
        </div>
      </header>
    </div>
  );
}

export default App;
