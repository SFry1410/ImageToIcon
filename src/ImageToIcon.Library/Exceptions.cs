namespace ImageToIcon.Library;

/// The library directory exists but the current account may not write to it.
public sealed class LibraryAccessDeniedException(string path, Exception? inner = null)
    : UnauthorizedAccessException(
        $"No permission to write to '{path}'. Run again with --elevate (or as administrator) to update the icon library.", inner);

/// The icon library could not be updated. The original library file is left untouched.
public sealed class LibraryUpdateException(string message, Exception? inner = null)
    : InvalidOperationException(message, inner);
