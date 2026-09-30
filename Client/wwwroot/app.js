// Saves text as a downloaded file (used by the JSON export). Kept in a file
// rather than an inline <script> so the Content-Security-Policy can forbid
// inline scripts.
window.mttSaveFile = (fileName, text) => {
    const blob = new Blob([text], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
};
