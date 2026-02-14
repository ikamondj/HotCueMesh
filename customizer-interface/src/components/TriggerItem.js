import React, { useMemo, useState } from 'react';

const maskHasColor = (mask, value) => (mask & value) === value;

function TriggerItem({
  trigger,
  hotcueTypes,
  cueMatchTypes,
  colorOptions,
  onChange,
  onRemove,
  onSelect,
  onAddAction,
  selected
}) {
  const [showColorSelector, setShowColorSelector] = useState(false);
  const [showHotcueTypes, setShowHotcueTypes] = useState(true);
  const [isExpanded, setIsExpanded] = useState(true);

  const allColorsMask = useMemo(
    () => colorOptions.reduce((accumulator, color) => accumulator | color.value, 0),
    [colorOptions]
  );

  const selectedColorsCount = useMemo(
    () => colorOptions.filter((color) => maskHasColor(trigger.cueColor, color.value)).length,
    [colorOptions, trigger.cueColor]
  );

  const selectedHotcueTypes = useMemo(() => {
    if (Array.isArray(trigger.hotcueTypes)) {
      return trigger.hotcueTypes;
    }

    if (typeof trigger.hotcueType === 'string' && trigger.hotcueType) {
      return [trigger.hotcueType];
    }

    return [];
  }, [trigger.hotcueType, trigger.hotcueTypes]);

  const updateField = (key, value) => {
    onChange(trigger.id, { [key]: value });
  };

  const toggleColor = (colorValue) => {
    const nextMask = maskHasColor(trigger.cueColor, colorValue)
      ? trigger.cueColor & ~colorValue
      : trigger.cueColor | colorValue;

    updateField('cueColor', nextMask);
  };

  const toggleHotcueType = (hotcueType) => {
    const isCurrentlySelected = selectedHotcueTypes.includes(hotcueType);
    const nextHotcueTypes = isCurrentlySelected
      ? selectedHotcueTypes.filter((type) => type !== hotcueType)
      : [...selectedHotcueTypes, hotcueType];

    onChange(trigger.id, {
      hotcueTypes: nextHotcueTypes,
      hotcueType: nextHotcueTypes[0] || ''
    });
  };

  const handleTriggerSelect = (event) => {
    if (event.target.closest('[data-trigger-control="true"]')) {
      return;
    }

    onSelect(trigger.id);
  };

  const handleTriggerKeyDown = (event) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      onSelect(trigger.id);
    }
  };

  const handleTriggerDoubleClick = (event) => {
    if (event.target.closest('[data-trigger-control="true"]')) {
      return;
    }

    setIsExpanded((previous) => !previous);
  };

  const collapsedTooltip =
    !isExpanded && typeof trigger.description === 'string' && trigger.description.trim().length > 0
      ? trigger.description.trim()
      : undefined;

  const displayName =
    typeof trigger.name === 'string' && trigger.name.trim().length > 0
      ? trigger.name.trim()
      : `Trigger ${trigger.id}`;

  const isCueMatchNone =
    !trigger.cueMatchType || String(trigger.cueMatchType).toLowerCase() === 'none';

  return (
    <div
      className={`item trigger${selected ? ' selected' : ''}`}
      onClick={handleTriggerSelect}
      onKeyDown={handleTriggerKeyDown}
      onDoubleClick={handleTriggerDoubleClick}
      role="button"
      tabIndex={0}
      title={collapsedTooltip}
    >
      <div className="item-head">
        <button
          type="button"
          className="trigger-name-button"
          data-trigger-control="true"
          onClick={() => setIsExpanded((previous) => !previous)}
          title={isExpanded ? 'Collapse trigger' : 'Expand trigger'}
        >
          {displayName}
        </button>
        <label className="checkbox-field" data-trigger-control="true">
          <input
            type="checkbox"
            data-trigger-control="true"
            checked={Boolean(trigger.enabled)}
            onChange={(event) => updateField('enabled', event.target.checked)}
          />
          <span>Enabled</span>
        </label>
        <button
          type="button"
          className="secondary-button"
          data-trigger-control="true"
          onClick={() => onRemove(trigger.id)}
        >
          Remove
        </button>
      </div>

      {isExpanded ? (
        <>
          <label className="field" data-trigger-control="true">
            <span className="field-label">Name</span>
            <input
              className="field-input"
              type="text"
              data-trigger-control="true"
              placeholder={`Trigger ${trigger.id}`}
              value={trigger.name || ''}
              onChange={(event) => updateField('name', event.target.value)}
            />
          </label>

          <label className="field" data-trigger-control="true">
            <span className="field-label">Description</span>
            <textarea
              className="field-input trigger-description-input"
              data-trigger-control="true"
              placeholder="Shown as tooltip while this trigger is collapsed"
              value={trigger.description || ''}
              onChange={(event) => updateField('description', event.target.value)}
            />
          </label>

          <div className="field" data-trigger-control="true">
            <div className="field-label-row">
              <span className="field-label">Hot Cue Type</span>
              <button
                type="button"
                className="secondary-button compact-button"
                data-trigger-control="true"
                onClick={() => setShowHotcueTypes((previous) => !previous)}
              >
                {showHotcueTypes ? 'Hide Types' : 'Show Types'} ({selectedHotcueTypes.length}/
                {hotcueTypes.length})
              </button>
            </div>
            {showHotcueTypes ? (
              <div className="hotcue-type-grid">
                {hotcueTypes.map((hotcueType) => (
                  <label key={hotcueType} className="hotcue-type-option" data-trigger-control="true">
                    <input
                      type="checkbox"
                      data-trigger-control="true"
                      checked={selectedHotcueTypes.includes(hotcueType)}
                      onChange={() => toggleHotcueType(hotcueType)}
                    />
                    <span>{hotcueType}</span>
                  </label>
                ))}
              </div>
            ) : null}
          </div>

          <label className="field" data-trigger-control="true">
            <span className="field-label">Cue Match Type</span>
            <select
              className="field-input"
              data-trigger-control="true"
              value={trigger.cueMatchType}
              onChange={(event) => updateField('cueMatchType', event.target.value)}
            >
              <option value="">None</option>
              {cueMatchTypes.map((cueMatchType) => (
                <option key={cueMatchType} value={cueMatchType}>
                  {cueMatchType}
                </option>
              ))}
            </select>
          </label>

          {!isCueMatchNone ? (
            <label className="field" data-trigger-control="true">
              <span className="field-label">Cue Name</span>
              <input
                className="field-input"
                type="text"
                data-trigger-control="true"
                placeholder="Enter cue name"
                value={trigger.cueName || ''}
                onChange={(event) => updateField('cueName', event.target.value)}
              />
            </label>
          ) : null}

          

          <div className="field" data-trigger-control="true">
            <span className="field-label">Cue Color</span>
            <div className="color-control-row">
              <button
                type="button"
                className="secondary-button color-toggle"
                data-trigger-control="true"
                onClick={() => setShowColorSelector((previous) => !previous)}
              >
                {showColorSelector ? 'Hide Colors' : 'Choose Colors'} ({selectedColorsCount}/
                {colorOptions.length})
              </button>
              <div className="selected-color-grid" aria-label="Selected cue color preview">
                {colorOptions.map((color) => {
                  const isSelected = maskHasColor(trigger.cueColor, color.value);
                  const isTransparent = color.hex === 'transparent';

                  return (
                    <span
                      key={color.value}
                      className={
                        'selected-color-cell' +
                        (isSelected ? ' selected' : '') +
                        (isTransparent ? ' transparent-color' : '')
                      }
                      style={isSelected && !isTransparent ? { backgroundColor: color.hex } : undefined}
                      title={color.label}
                    />
                  );
                })}
              </div>
              <button
                type="button"
                className="primary-button add-action-button"
                data-trigger-control="true"
                onClick={() => onAddAction(trigger.id)}
              >
                Add Action
              </button>
            </div>

            {showColorSelector ? (
              <div className="color-picker">
                <div className="color-picker-actions">
                  <button
                    type="button"
                    className="secondary-button"
                    data-trigger-control="true"
                    onClick={() => updateField('cueColor', allColorsMask)}
                  >
                    Select All
                  </button>
                  <button
                    type="button"
                    className="secondary-button"
                    data-trigger-control="true"
                    onClick={() => updateField('cueColor', 0)}
                  >
                    Deselect All
                  </button>
                </div>
                <div className="color-grid">
                  {colorOptions.map((color) => (
                    <label key={color.value} className="color-option">
                      <input
                        type="checkbox"
                        data-trigger-control="true"
                        checked={maskHasColor(trigger.cueColor, color.value)}
                        onChange={() => toggleColor(color.value)}
                      />
                      <span className="color-chip" style={{ backgroundColor: color.hex }} />
                      <span>{color.label}</span>
                    </label>
                  ))}
                </div>
              </div>
            ) : null}
          </div>
        </>
      ) : null}

    </div>
  );
}

export default TriggerItem;
