mergeInto(LibraryManager.library, {
  PochinkiSendInputJson: function (jsonPointer) {
    try {
      var payload = JSON.parse(UTF8ToString(jsonPointer));

      window.parent.postMessage(
        {
          type: 'pochinki:unity-input',
          payload: payload
        },
        window.location.origin
      );
    } catch (error) {
      console.error('[Pochinki Multiplayer] Unable to send Unity input', error);
    }
  },
});
