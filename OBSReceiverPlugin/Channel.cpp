// Channel.cpp
#include "Channel.hpp"

bool StringChannel::push(std::string msg) {
    {
        std::lock_guard<std::mutex> lock(m_);
        if (closed_) return false;
        q_.push_back(std::move(msg));
    }
    cv_.notify_one();
    return true;
}

bool StringChannel::pop(std::string& out) {
    std::unique_lock<std::mutex> lock(m_);
    cv_.wait(lock, [&] { return closed_ || !q_.empty(); });

    if (q_.empty()) return false;

    out = std::move(q_.front());
    q_.pop_front();
    return true;
}

void StringChannel::close() {
    {
        std::lock_guard<std::mutex> lock(m_);
        closed_ = true;
    }
    cv_.notify_all();
}

bool StringChannel::is_closed() const {
    std::lock_guard<std::mutex> lock(m_);
    return closed_;
}
