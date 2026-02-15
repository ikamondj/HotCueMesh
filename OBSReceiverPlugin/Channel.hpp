// Channel.hpp
#pragma once

#include <condition_variable>
#include <deque>
#include <mutex>
#include <string>

class StringChannel {
public:
    StringChannel() = default;
    StringChannel(const StringChannel&) = delete;
    StringChannel& operator=(const StringChannel&) = delete;

    // Returns false if channel is closed (message not accepted).
    bool push(std::string msg);

    // Blocks until a message is available or the channel is closed+empty.
    // Returns true if a message was popped, false if closed+empty.
    bool pop(std::string& out);

    // Close the channel. Unblocks pop(). Further push() calls return false.
    void close();

    bool is_closed() const;

private:
    mutable std::mutex m_;
    std::condition_variable cv_;
    std::deque<std::string> q_;
    bool closed_ = false;
};
