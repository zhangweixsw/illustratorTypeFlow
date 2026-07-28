#include "PipePublisher.h"

#include <Windows.h>

#include <chrono>
#include <cstdio>
#include <string>

namespace
{
constexpr wchar_t kPipePath[] = LR"(\\.\pipe\IllustratorTypeFlow.v1)";

const char* StateName(const CanvasState state)
{
    switch (state)
    {
        case CanvasState::CanvasTextEditing:
            return "CanvasTextEditing";
        case CanvasState::NotEditing:
            return "NotEditing";
        default:
            return "Unavailable";
    }
}

long long UnixMilliseconds()
{
    return std::chrono::duration_cast<std::chrono::milliseconds>(
               std::chrono::system_clock::now().time_since_epoch())
        .count();
}
}

PipePublisher::PipePublisher() = default;

PipePublisher::~PipePublisher()
{
    Stop();
}

void PipePublisher::Start()
{
    if (worker_.joinable())
        return;

    stop_.store(false);
    worker_ = std::thread(&PipePublisher::Run, this);
}

void PipePublisher::Stop()
{
    stop_.store(true);
    if (worker_.joinable())
        worker_.join();
}

void PipePublisher::SetState(const CanvasState state) noexcept
{
    state_.store(state);
}

void PipePublisher::Run()
{
    CanvasState previous = CanvasState::Unavailable;
    auto lastHeartbeat = std::chrono::steady_clock::time_point::min();

    while (!stop_.load())
    {
        const auto current = state_.load();
        const auto now = std::chrono::steady_clock::now();
        if (current != previous || now - lastHeartbeat >= std::chrono::milliseconds(500))
        {
            if (Publish(current))
            {
                previous = current;
                lastHeartbeat = now;
            }
        }

        std::this_thread::sleep_for(std::chrono::milliseconds(50));
    }
}

bool PipePublisher::Publish(const CanvasState state) const
{
    const HANDLE pipe = CreateFileW(
        kPipePath,
        GENERIC_WRITE,
        0,
        nullptr,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);
    if (pipe == INVALID_HANDLE_VALUE)
        return false;

    char payload[256]{};
    const int length = std::snprintf(
        payload,
        sizeof(payload),
        R"({"protocol":1,"state":"%s","pid":%lu,"timestamp":%lld})"
        "\n",
        StateName(state),
        GetCurrentProcessId(),
        UnixMilliseconds());

    DWORD written = 0;
    const BOOL succeeded = length > 0 &&
                           WriteFile(pipe, payload, static_cast<DWORD>(length), &written, nullptr);
    CloseHandle(pipe);
    return succeeded != FALSE && written == static_cast<DWORD>(length);
}

