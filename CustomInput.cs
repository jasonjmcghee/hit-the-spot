using Godot;

public static class CustomInput {
    public static void EnsureActionKey(string action, KeyList scancode) {
        EnsureActionEvent(action, new InputEventKey { Scancode = (uint)scancode });
    }

    public static void EnsureActionEvent(string action, InputEvent ev, bool replace = false) {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);

        if (replace) InputMap.ActionEraseEvents(action);

        InputMap.ActionAddEvent(action, ev);
    }
}