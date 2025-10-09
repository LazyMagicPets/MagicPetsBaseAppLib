# Chat Integration Design for BaseAppLib

## Architecture Overview

The existing BaseAppLib pattern follows:
- **ViewModels** in `BaseApp.ViewModels/Session/` using LazyMagic's `LzItemViewModel<T>` and `LzItemsViewModel<T>` base classes
- **Components** in `BaseApp.BlazorUI/Components/` for reusable UI elements
- **Pages** in `BlazorUI/Pages/` for full page layouts
- **Session integration** via `BaseAppSessionViewModel` which aggregates all feature ViewModels
- **API clients** injected via factories (`IChatModuleClient` already registered)

## Proposed Implementation Structure

### 1. ViewModels (BaseApp.ViewModels)

**Location**: `/BaseApp.ViewModels/Session/Chat/`

#### Files to Create

##### ChatViewModel.cs
- **Inherits**: `LzItemViewModel<Chat, ChatModel>`
- **Purpose**: Manages individual chat session
- **Key Features**:
  - Properties: Chat metadata (title, created date, etc.)
  - Access to messages via `ChatMessagesViewModel`
  - SendMessage command/method
  - Delete command

##### ChatsViewModel.cs
- **Inherits**: `LzItemsViewModel<ChatViewModel, Chat, ChatModel>`
- **Purpose**: Manages list of all user chats
- **Key Features**:
  - List all chats for user
  - Create new chat
  - Select current/active chat
  - Factory injection for `ChatViewModel` instances

##### ChatMessageViewModel.cs
- **Inherits**: `LzItemViewModel<ChatMessage, ChatMessageModel>`
- **Purpose**: Individual message in a chat
- **Key Features**:
  - Message content, role (user/assistant), timestamp
  - Streaming state for assistant responses
  - Markdown rendering support

##### ChatMessagesViewModel.cs
- **Inherits**: `LzItemsViewModel<ChatMessageViewModel, ChatMessage, ChatMessageModel>`
- **Purpose**: Message history for a specific chat
- **Key Features**:
  - Load/paginate message history
  - Append new messages (user and assistant)
  - Handle streaming responses
  - Real-time updates via AppSync events

#### Updates Required

**BaseAppSessionViewModel.cs** and **IBaseAppSessionViewModel.cs**:
- Add `ChatsViewModel ChatsViewModel { get; set; }` property
- Inject `IChatsViewModelFactory` in constructor

### 2. Components (BaseApp.BlazorUI)

**Location**: `/BaseApp.BlazorUI/Components/`

#### Files to Create

##### ChatsList.razor
- **Purpose**: Display list of user's chats
- **Parameters**:
  - `ChatsViewModel ViewModel`
  - `EventCallback<ChatModel> OnChatSelected`
  - `EventCallback OnNewChat`
  - `EventCallback<ChatModel> OnDeleteChat`
- **UI**: MudTable or MudList showing chat titles, dates
- **Actions**: Select, create new, delete

##### ChatMessageList.razor
- **Purpose**: Display chat message history
- **Parameters**:
  - `ChatMessagesViewModel ViewModel`
- **UI**: Scrollable message list with user/assistant styling
- **Features**:
  - Auto-scroll to bottom on new messages
  - Markdown rendering for assistant responses
  - Loading indicator for streaming
  - Timestamp display

##### ChatInput.razor
- **Purpose**: Message input field with send button
- **Parameters**:
  - `EventCallback<string> OnSendMessage`
  - `bool IsDisabled` (for streaming state)
- **UI**: MudTextField with Send button/icon
- **Features**:
  - Enter key to send
  - Disable during streaming
  - Clear after send

##### ChatContainer.razor (Optional)
- **Purpose**: Full chat UI combining messages + input
- **Parameters**:
  - `ChatViewModel ViewModel`
- **Combines**: `ChatMessageList` and `ChatInput`

### 3. Pages (BlazorUI/Pages)

**Location**: `/BlazorUI/Pages/Chat/`

#### Files to Create

##### ChatsPage.razor
- **Route**: `/ChatsPage`
- **Purpose**: List all chats, create new, select
- **ViewModel**: `ChatsViewModel` from `SessionViewModel.ChatsViewModel`
- **Layout**: Uses `ChatsList` component
- **Navigation**: Routes to `ChatPage` when chat selected

##### ChatPage.razor
- **Route**: `/ChatPage`
- **Purpose**: Active chat interface
- **ViewModel**: `ChatViewModel` from `SessionViewModel.ChatsViewModel.CurrentViewModel`
- **Layout**: Uses `ChatMessageList` and `ChatInput` (or `ChatContainer`)
- **Features**:
  - Send messages
  - Receive streaming responses
  - Back to chats list navigation

### 4. Integration Points

#### API Client Setup
- `IChatModuleClient` already registered in `ConfigureBaseAppViewModels.cs:22`
- Applications need to register actual client implementation

#### Navigation
- Add chat links to main navigation/menu
- Flow: ChatsPage → ChatPage (on selection)

#### AppSync Events (for real-time updates)
- Subscribe to chat message events in `ChatMessagesViewModel`
- Update UI when assistant responses stream in
- Handle message completion events

## Implementation Order

### Phase 1: ViewModels Layer (Bottom-Up)
1. `ChatMessageViewModel.cs`
2. `ChatMessagesViewModel.cs`
3. `ChatViewModel.cs`
4. `ChatsViewModel.cs`
5. Update `IBaseAppSessionViewModel` interface
6. Update `BaseAppSessionViewModel` implementation

### Phase 2: Components Layer
1. `ChatInput.razor`
2. `ChatMessageList.razor`
3. `ChatsList.razor`
4. (Optional) `ChatContainer.razor`

### Phase 3: Pages Layer
1. `ChatPage.razor`
2. `ChatsPage.razor`

### Phase 4: Testing
1. Wire up actual `IChatModuleClient` in consuming app (StoreApp, ConsumerApp, AdminApp)
2. Test create chat, send messages, streaming responses
3. Test navigation flow

## Key Patterns to Follow

✓ Use `[Factory]` attribute on ViewModels with `[FactoryInject]` for dependencies
✓ Inherit from `LzItemViewModel<TDTO, TModel>` for single items
✓ Inherit from `LzItemsViewModel<TViewModel, TDTO, TModel>` for collections
✓ Set `_DTOCreateAsync`, `_DTOReadAsync`, `_DTOUpdateAsync`, `_DTODeleteAsync` delegates in constructors
✓ Components use `LzComponentBasePassViewModel<T>` or `LzComponentBaseAssignViewModel<T>`
✓ Pages assign ViewModel from `SessionViewModel` in `OnInitializedAsync`
✓ Use `EventCallback` for component events
✓ MudBlazor components for UI (MudTable, MudList, MudTextField, MudButton, etc.)

## Summary

**Implementation Scope**:
- **4 ViewModels** in `BaseApp.ViewModels/Session/Chat/`
- **3-4 Components** in `BaseApp.BlazorUI/Components/`
- **2 Pages** in `BlazorUI/Pages/Chat/`
- **Session integration** via updated `BaseAppSessionViewModel`
- **No changes** needed to MAUIApp or WASMApp (as requested)

The implementation follows LazyMagic's factory pattern, ReactiveUI MVVM, and MudBlazor components, matching the existing Pets feature architecture.
