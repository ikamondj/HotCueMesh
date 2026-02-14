import React from 'react';

const APP_ID_OPTIONS = ['OBS', 'lightingController', 'udp', 'tcp', 'http'];

const OBS_ACTION_TYPES = [
  'display source',
  'hide source',
  'toggle source visibility',
  'enable filter',
  'disable filter',
  'toggle filter',
  'change to scene'
];

const LIGHTING_ACTION_TYPES = ['set lighting preset', 'set random preset from numeric list'];

const NETWORK_APP_IDS = new Set(['udp', 'tcp', 'http']);

const parsePresetList = (rawValue) => rawValue.split(/[,\s]+/).filter(Boolean);

function ActionItem({ action, onChange, onRemove }) {
  const updateField = (key, value) => {
    onChange(action.id, { [key]: value });
  };

  const handleAppIdChange = (nextAppId) => {
    if (nextAppId === 'OBS') {
      onChange(action.id, { appId: nextAppId, actionType: OBS_ACTION_TYPES[0] });
      return;
    }

    if (nextAppId === 'lightingController') {
      onChange(action.id, { appId: nextAppId, actionType: LIGHTING_ACTION_TYPES[0] });
      return;
    }

    onChange(action.id, { appId: nextAppId, actionType: 'send payload' });
  };

  const isObsAction = action.appId === 'OBS';
  const isLightingAction = action.appId === 'lightingController';
  const isNetworkAction = NETWORK_APP_IDS.has(action.appId);

  const isObsSourceAction =
    action.actionType === 'display source' ||
    action.actionType === 'hide source' ||
    action.actionType === 'toggle source visibility';

  const isObsFilterAction =
    action.actionType === 'enable filter' ||
    action.actionType === 'disable filter' ||
    action.actionType === 'toggle filter';

  const isObsSceneAction = action.actionType === 'change to scene';

  const parsedPort = Number(action.targetPort);
  const hasPortValue = String(action.targetPort).trim() !== '';
  const isPortInvalid = hasPortValue && (!Number.isInteger(parsedPort) || parsedPort < 1 || parsedPort > 65535);

  const presetTokens = parsePresetList(action.randomPresetList || '');
  const hasPresetListValue = String(action.randomPresetList || '').trim() !== '';
  const hasInvalidPresetToken = presetTokens.some((token) => {
    if (!/^\d+$/.test(token)) {
      return true;
    }

    const parsed = Number(token);
    return parsed < 0 || parsed > 128;
  });

  const showPresetListValidation =
    isLightingAction &&
    action.actionType === 'set random preset from numeric list' &&
    hasPresetListValue &&
    hasInvalidPresetToken;

  return (
    <div className="item action">
      <div className="action-head">
        <div className="item-title">Action {action.id}</div>
        <button
          type="button"
          className="action-delete-button"
          aria-label={`Delete action ${action.id}`}
          onClick={() => onRemove(action.id)}
          title="Delete action"
        >
          X
        </button>
      </div>

      <label className="field">
        <span className="field-label">App ID</span>
        <select
          className="field-input"
          value={action.appId}
          onChange={(event) => handleAppIdChange(event.target.value)}
        >
          {APP_ID_OPTIONS.map((appIdOption) => (
            <option key={appIdOption} value={appIdOption}>
              {appIdOption}
            </option>
          ))}
        </select>
      </label>

      {isObsAction ? (
        <>
          <label className="field">
            <span className="field-label">ActionType</span>
            <select
              className="field-input"
              value={action.actionType}
              onChange={(event) => updateField('actionType', event.target.value)}
            >
              {OBS_ACTION_TYPES.map((actionType) => (
                <option key={actionType} value={actionType}>
                  {actionType}
                </option>
              ))}
            </select>
          </label>

          {isObsSourceAction || isObsFilterAction ? (
            <label className="field">
              <span className="field-label">Source Name</span>
              <input
                type="text"
                className="field-input"
                value={action.sourceName}
                onChange={(event) => updateField('sourceName', event.target.value)}
                placeholder="Camera, BrowserSource, etc."
              />
            </label>
          ) : null}

          {isObsFilterAction ? (
            <label className="field">
              <span className="field-label">Filter Name</span>
              <input
                type="text"
                className="field-input"
                value={action.filterName}
                onChange={(event) => updateField('filterName', event.target.value)}
                placeholder="ColorCorrection, Blur, etc."
              />
            </label>
          ) : null}

          {isObsSceneAction ? (
            <label className="field">
              <span className="field-label">Scene Name</span>
              <input
                type="text"
                className="field-input"
                value={action.sceneName}
                onChange={(event) => updateField('sceneName', event.target.value)}
                placeholder="Main Scene"
              />
            </label>
          ) : null}
        </>
      ) : null}

      {isLightingAction ? (
        <>
          <label className="field">
            <span className="field-label">ActionType</span>
            <select
              className="field-input"
              value={action.actionType}
              onChange={(event) => updateField('actionType', event.target.value)}
            >
              {LIGHTING_ACTION_TYPES.map((actionType) => (
                <option key={actionType} value={actionType}>
                  {actionType}
                </option>
              ))}
            </select>
          </label>

          {action.actionType === 'set lighting preset' ? (
            <label className="field">
              <span className="field-label">Preset (0-128)</span>
              <input
                type="number"
                min="0"
                max="128"
                step="1"
                className="field-input"
                value={action.lightingPreset}
                onChange={(event) => {
                  const nextRaw = event.target.value;

                  if (nextRaw === '') {
                    updateField('lightingPreset', '');
                    return;
                  }

                  const nextValue = Number(nextRaw);
                  const clampedValue = Number.isNaN(nextValue)
                    ? 0
                    : Math.min(128, Math.max(0, nextValue));
                  updateField('lightingPreset', clampedValue);
                }}
              />
            </label>
          ) : null}

          {action.actionType === 'set random preset from numeric list' ? (
            <label className="field">
              <span className="field-label">Preset List</span>
              <input
                type="text"
                className="field-input"
                value={action.randomPresetList}
                onChange={(event) => updateField('randomPresetList', event.target.value)}
                placeholder="Example: 1, 4, 8 32"
              />
              <span className="field-help">Use comma or space separated preset numbers.</span>
              {showPresetListValidation ? (
                <span className="field-error">Only numbers from 0 to 128 are allowed.</span>
              ) : null}
            </label>
          ) : null}
        </>
      ) : null}

      {isNetworkAction ? (
        <>
          <label className="field">
            <span className="field-label">Target Host</span>
            <input
              type="text"
              className="field-input"
              value={action.targetHost}
              onChange={(event) => updateField('targetHost', event.target.value)}
              placeholder="192.168.1.100 or example.com"
            />
          </label>

          <label className="field">
            <span className="field-label">Port</span>
            <input
              type="number"
              min="1"
              max="65535"
              step="1"
              className="field-input"
              value={action.targetPort}
              onChange={(event) => updateField('targetPort', event.target.value)}
              placeholder="8000"
            />
            {isPortInvalid ? <span className="field-error">Port must be between 1 and 65535.</span> : null}
          </label>

          {action.appId === 'http' ? (
            <>
              <label className="field">
                <span className="field-label">Path</span>
                <input
                  type="text"
                  className="field-input"
                  value={action.endpointPath}
                  onChange={(event) => updateField('endpointPath', event.target.value)}
                  placeholder="/api/trigger"
                />
              </label>
            </>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

export default ActionItem;
