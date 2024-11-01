let Reference = {};
Reference.DotNet = null;

const keyDownHandler = (e) => {
    if (e.repeat) return;
    Reference.DotNet.invokeMethodAsync('OnKeyDown', e.key);
}

export function SetDotNetReference(dotnet) {
    Reference.DotNet = dotnet;

    window.addEventListener('keydown', keyDownHandler);
}

export function Dispose() {
    window.removeEventListener('keydown', keyDownHandler);
}