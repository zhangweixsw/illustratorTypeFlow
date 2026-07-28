#pragma once

#include <atomic>
#include <thread>

enum class CanvasState : int
{
    Unavailable = 0,
    NotEditing = 1,
    CanvasTextEditing = 2
};

class PipePublisher final
{
public:
    PipePublisher();
    ~PipePublisher();

    PipePublisher(const PipePublisher&) = delete;
    PipePublisher& operator=(const PipePublisher&) = delete;

    void Start();
    void Stop();
    void SetState(CanvasState state) noexcept;

private:
    void Run();
    bool Publish(CanvasState state) const;

    std::atomic<CanvasState> state_{CanvasState::Unavailable};
    std::atomic<bool> stop_{false};
    std::thread worker_;
};

