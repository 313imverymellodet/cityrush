mergeInto(LibraryManager.library, {
  SD_Rewarded: function (goPtr) {
    var go = UTF8ToString(goPtr);
    var reply = function (ok) { try { window.unityInstance && window.unityInstance.SendMessage(go, "OnRewarded", ok ? "1" : "0"); } catch (e) {} };
    if (window.SD && window.SD.rewarded) window.SD.rewarded().then(function (ok) { reply(ok); }, function () { reply(false); });
    else reply(false);
  },
  SD_AdsAvailable: function () { return (window.SD && window.SD.adsAvailable && window.SD.adsAvailable()) ? 1 : 0; },
  SD_Gameplay: function (on) { if (window.SD && window.SD.gameplay) window.SD.gameplay(!!on); },
  SD_Event: function (namePtr, value) { if (window.SD && window.SD.track) window.SD.track(UTF8ToString(namePtr), value); },
  SD_Ready: function () { if (window.SD && window.SD.ready) window.SD.ready(); },

  CR_Vibrate: function (ms) { try { if (navigator.vibrate && (!navigator.userActivation || navigator.userActivation.hasBeenActive)) navigator.vibrate(ms); } catch (e) {} },

  CR_ArmShare: function (textPtr) {
    var text = UTF8ToString(textPtr), w = window;
    var url = w.location.origin + w.location.pathname;
    var doShare = function () {
      if (!w.__crPending) return;
      var t = w.__crPending; w.__crPending = null;
      if (navigator.share) navigator.share({ title: "CITY RUSH", text: t, url: url }).catch(function () {});
      else if (navigator.clipboard) navigator.clipboard.writeText(t + "\n" + url).then(function () { w.crToast && w.crToast("Copied! Paste it anywhere"); });
      if (w.SD && w.SD.track) w.SD.track("share", 0);
    };
    if (!w.__crHooked) {
      w.__crHooked = true;
      ["pointerup", "touchend", "click"].forEach(function (ev) { w.addEventListener(ev, doShare, true); });
    }
    w.__crPending = text;
    setTimeout(doShare, 450);
  },

  CR_RaceStart: function (mapPtr) { if (window.cityRace) window.cityRace.start(UTF8ToString(mapPtr)); },
  CR_RaceSubmit: function (mapPtr, ms, lapMs, carPtr) { if (window.cityRace) window.cityRace.submit(UTF8ToString(mapPtr), ms, lapMs, UTF8ToString(carPtr)); },
  CR_ShowBoard: function (mapPtr) { if (window.cityRace) window.cityRace.board(UTF8ToString(mapPtr)); },
  CR_NetOpen: function (mapPtr, carPtr) { if (window.cityRace) window.cityRace.lobby(UTF8ToString(mapPtr), UTF8ToString(carPtr)); },
  CR_NetState: function (x, z, h, v, lap, s) { if (window.cityRace) window.cityRace.state(x, z, h, v, lap, s); },
  CR_NetFinish: function (ms) { if (window.cityRace) window.cityRace.finish(ms); },
  CR_NetLeave: function () { if (window.cityRace) window.cityRace.leave(); }
});
