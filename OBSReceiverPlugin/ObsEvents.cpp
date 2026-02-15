#include "ObsEvents.hpp"
#include <obs-module.h>
#include <cstdint>
#include <string_view>

namespace {

constexpr bool is_ws(const char c) noexcept
{
	return c == ' ' || c == '\t' || c == '\n' || c == '\r';
}

inline void trim_ascii_ws(std::string_view &text) noexcept
{
	while (!text.empty() && is_ws(text.front()))
		text.remove_prefix(1);
	while (!text.empty() && is_ws(text.back()))
		text.remove_suffix(1);
}

inline std::string_view next_token(std::string_view text, size_t &cursor) noexcept
{
	const size_t n = text.size();
	while (cursor < n && is_ws(text[cursor]))
		++cursor;

	const size_t start = cursor;
	while (cursor < n && !is_ws(text[cursor]))
		++cursor;

	return text.substr(start, cursor - start);
}

enum class EventType : uint8_t {
	ShowSource,
	HideSource,
	ToggleSource,
	ShowFilter,
	HideFilter,
	ToggleFilter,
	SwitchScene,
	Unknown,
};

inline EventType parse_event_type(const std::string_view token) noexcept
{
	switch (token.size()) {
	case 11:
		if (token == "show_source")
			return EventType::ShowSource;
		if (token == "hide_source")
			return EventType::HideSource;
		break;
	case 12:
		if (token == "show_filter")
			return EventType::ShowFilter;
		if (token == "hide_filter")
			return EventType::HideFilter;
		if (token == "switch_scene")
			return EventType::SwitchScene;
		break;
	case 13:
		if (token == "toggle_source")
			return EventType::ToggleSource;
		if (token == "toggle_filter")
			return EventType::ToggleFilter;
		break;
	}

	return EventType::Unknown;
}

struct ParsedEventArgs {
	std::string_view scene_name;
	std::string_view source_name;
	std::string_view filter_name;
};

inline bool parse_event_segment(std::string_view segment, EventType &out_type,
				std::string_view &out_type_token, ParsedEventArgs &out_args) noexcept
{
	trim_ascii_ws(segment);
	if (segment.empty())
		return false;

	size_t cursor = 0;
	out_type_token = next_token(segment, cursor);
	if (out_type_token.empty())
		return false;

	out_type = parse_event_type(out_type_token);
	out_args = {};

	while (true) {
		const std::string_view token = next_token(segment, cursor);
		if (token.empty())
			break;

		if (token.front() != '-') {
			continue;
		}

		const std::string_view key = token.substr(1);
		const size_t value_cursor = cursor;
		std::string_view value = next_token(segment, cursor);
		if (!value.empty() && value.front() == '-') {
			cursor = value_cursor;
			value = {};
		}

		if (key == "scene_name") {
			out_args.scene_name = value;
		} else if (key == "source_name") {
			out_args.source_name = value;
		} else if (key == "filter_name") {
			out_args.filter_name = value;
		}
	}

	return true;
}

} // namespace

void on_hot_cue_event(const std::string& event, StringChannel& channel) {
	static_cast<void>(channel);
	blog(LOG_INFO, "[hot-cue-mesh] received event: %s", event.c_str());
	process_event(event);
}

void process_event(const std::string& event) {
	std::string_view remaining(event);

	while (!remaining.empty()) {
		const size_t separator = remaining.find(';');
		const std::string_view segment =
			(separator == std::string_view::npos) ? remaining
							      : remaining.substr(0, separator);

		if (separator == std::string_view::npos) {
			remaining = {};
		} else {
			remaining.remove_prefix(separator + 1);
		}

		EventType type = EventType::Unknown;
		std::string_view type_token;
		ParsedEventArgs args{};
		if (!parse_event_segment(segment, type, type_token, args)) {
			continue;
		}

		static_cast<void>(args);

		switch (type) {
		case EventType::ShowSource:
		case EventType::HideSource:
		case EventType::ToggleSource:
		case EventType::ShowFilter:
		case EventType::HideFilter:
		case EventType::ToggleFilter:
		case EventType::SwitchScene:
			break;
		case EventType::Unknown:
			blog(LOG_WARNING, "[hot-cue-mesh] unknown event type: %.*s",
			     static_cast<int>(type_token.size()), type_token.data());
			break;
		}
	}
}
