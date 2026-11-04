#pragma once

namespace f4forge::prisma_ui
{
template <class Api, class Handle, class Enqueue>
void QueueDestruction(Api* renderer, Handle handle, Enqueue&& enqueue)
{
    // Cleanup outlives the event runtime and channel. Capture only the renderer
    // (which remains loaded for the process lifetime) and its view handle.
    enqueue([renderer, handle]() noexcept {
        if (renderer != nullptr && renderer->IsValid(handle))
        {
            renderer->Unfocus(handle);
            renderer->Destroy(handle);
        }
    });
}
}
