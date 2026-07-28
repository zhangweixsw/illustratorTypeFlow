#include "IllustratorSDK.h"

#include "AIDocument.h"
#include "AIArt.h"
#include "AINotifier.h"
#include "AITool.h"
#include "SPAccess.h"
#include "SPInterf.h"

#include "PipePublisher.h"

#include <cstring>

namespace
{
SPBasicSuite* gBasic = nullptr;
AIDocumentSuite* gDocument = nullptr;
AINotifierSuite* gNotifier = nullptr;

AINotifierHandle gSelectionNotifier = nullptr;
AINotifierHandle gToolNotifier = nullptr;
AINotifierHandle gDocumentNotifier = nullptr;

SPPluginRef gPlugin = nullptr;
PipePublisher gPublisher;

ASErr AcquireSuites()
{
    ASErr error = gBasic->AcquireSuite(
        kAIDocumentSuite,
        kAIDocumentSuiteVersion,
        reinterpret_cast<const void**>(&gDocument));
    if (error != kNoErr)
        return error;

    return gBasic->AcquireSuite(
        kAINotifierSuite,
        kAINotifierSuiteVersion,
        reinterpret_cast<const void**>(&gNotifier));
}

void ReleaseSuites()
{
    if (gBasic == nullptr)
        return;
    if (gNotifier != nullptr)
        gBasic->ReleaseSuite(kAINotifierSuite, kAINotifierSuiteVersion);
    if (gDocument != nullptr)
        gBasic->ReleaseSuite(kAIDocumentSuite, kAIDocumentSuiteVersion);
    gNotifier = nullptr;
    gDocument = nullptr;
}

ASErr AddNotifiers()
{
    ASErr error = gNotifier->AddNotifier(
        gPlugin,
        "IllustratorTypeFlow selection",
        kAIArtSelectionChangedNotifier,
        &gSelectionNotifier);
    if (error != kNoErr)
        return error;

    error = gNotifier->AddNotifier(
        gPlugin,
        "IllustratorTypeFlow tool",
        kAIUserToolChangedNotifier,
        &gToolNotifier);
    if (error != kNoErr)
        return error;

    return gNotifier->AddNotifier(
        gPlugin,
        "IllustratorTypeFlow document",
        kAIDocumentChangedNotifier,
        &gDocumentNotifier);
}

void UpdateCanvasState()
{
    if (gDocument == nullptr)
    {
        gPublisher.SetState(CanvasState::Unavailable);
        return;
    }

    AIBoolean hasTextFocus = false;
    const ASErr error = gDocument->HasTextFocus(&hasTextFocus);
    if (error != kNoErr)
    {
        gPublisher.SetState(CanvasState::Unavailable);
        return;
    }

    gPublisher.SetState(
        hasTextFocus ? CanvasState::CanvasTextEditing : CanvasState::NotEditing);
}

ASErr Startup(SPInterfaceMessage* message)
{
    gBasic = message->d.basic;
    gPlugin = message->d.self;

    ASErr error = AcquireSuites();
    if (error != kNoErr)
        return error;

    error = AddNotifiers();
    if (error != kNoErr)
    {
        ReleaseSuites();
        return error;
    }

    UpdateCanvasState();
    gPublisher.Start();
    return kNoErr;
}

ASErr Shutdown()
{
    gPublisher.SetState(CanvasState::Unavailable);
    gPublisher.Stop();
    ReleaseSuites();
    gPlugin = nullptr;
    gBasic = nullptr;
    return kNoErr;
}
}

extern "C" __declspec(dllexport) ASAPI ASErr PluginMain(
    char* caller,
    char* selector,
    void* message)
{
    if (caller == nullptr || selector == nullptr || message == nullptr)
        return kBadParameterErr;

    if (std::strcmp(caller, kSPInterfaceCaller) == 0)
    {
        if (std::strcmp(selector, kSPInterfaceStartupSelector) == 0)
            return Startup(static_cast<SPInterfaceMessage*>(message));
        if (std::strcmp(selector, kSPInterfaceShutdownSelector) == 0)
            return Shutdown();
    }
    else if (std::strcmp(caller, kCallerAINotify) == 0 &&
             std::strcmp(selector, kSelectorAINotify) == 0)
    {
        // Illustrator SDK calls arrive on Illustrator's UI thread. Only this
        // callback touches suites; the pipe worker publishes the cached value.
        UpdateCanvasState();
    }

    return kNoErr;
}
