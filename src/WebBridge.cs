using System;

namespace SoundCloudDesktop
{
    internal static class WebBridge
    {
        public static readonly string InitScript = @"
(function () {
    if (window.__scDesktopInit) return;
    window.__scDesktopInit = true;

    const STYLE_ID = 'sc-desktop-injected-style';
    const THEME_IDS = ['dark', 'amoled', 'light', 'cyber', 'ocean'];

    if (typeof window.__scDesktopThemeId !== 'string') {
        window.__scDesktopThemeId = 'original';
    }
    if (typeof window.__scDesktopAdBlock !== 'boolean') {
        window.__scDesktopAdBlock = true;
    }
    if (typeof window.__scDesktopAudioQuality !== 'string') {
        window.__scDesktopAudioQuality = 'high';
    }

    function normalizeThemeId(themeId) {
        if (typeof themeId !== 'string') return 'original';
        const normalized = themeId.trim().toLowerCase();
        return THEME_IDS.indexOf(normalized) !== -1 ? normalized : 'original';
    }

    function applyThemeClass(themeId) {
        const resolved = normalizeThemeId(themeId);
        window.__scDesktopThemeId = resolved;
        const root = document.documentElement;
        if (!root || !root.classList) return;

        const classes = Array.prototype.slice.call(root.classList);
        for (let i = 0; i < classes.length; i++) {
            if (classes[i].indexOf('sc-theme-') === 0) {
                root.classList.remove(classes[i]);
            }
        }
        if (resolved !== 'original') {
            root.classList.add('sc-theme-' + resolved);
        }
    }

    function syncThemeClass() {
        const root = document.documentElement;
        if (!root || !root.classList) return;
        const resolved = normalizeThemeId(window.__scDesktopThemeId);
        const expected = resolved !== 'original' ? ('sc-theme-' + resolved) : null;
        const classes = Array.prototype.slice.call(root.classList);
        let hasExpected = expected === null;
        let hasStale = false;
        for (let i = 0; i < classes.length; i++) {
            const cls = classes[i];
            if (cls.indexOf('sc-theme-') === 0) {
                if (cls === expected) {
                    hasExpected = true;
                } else {
                    hasStale = true;
                }
            }
        }
        if (!hasExpected || hasStale) {
            applyThemeClass(resolved);
        }
    }

    function ensureStyle() {
        syncThemeClass();
        if (document.getElementById(STYLE_ID)) return;
        const style = document.createElement('style');
        style.id = STYLE_ID;
        style.textContent = `
            /* Custom scrollbar (defaults to SoundCloud dark look for 'original') */
            ::-webkit-scrollbar { width: 10px; height: 10px; }
            ::-webkit-scrollbar-track { background: var(--sc-scroll-track, #141414); }
            ::-webkit-scrollbar-thumb {
                background: var(--sc-scroll-thumb, #383838);
                border-radius: 5px;
                border: 2px solid var(--sc-scroll-track, #141414);
            }
            ::-webkit-scrollbar-thumb:hover { background: var(--sc-accent, #ff5500); }

            /* Theme token definitions (never active for 'original') */
            html.sc-theme-dark {
                color-scheme: dark;
                --sc-bg: #111113;
                --sc-surface: #18181b;
                --sc-surface-2: #222226;
                --sc-border: #2d2d32;
                --sc-text: #ececef;
                --sc-muted: #9a9aa2;
                --sc-accent: #ff5500;
                --sc-accent-rgb: 255, 85, 0;
                --sc-accent-text: #ffffff;
                --sc-scroll-track: #111113;
                --sc-scroll-thumb: #35353b;
            }
            html.sc-theme-amoled {
                color-scheme: dark;
                --sc-bg: #000000;
                --sc-surface: #0a0a0a;
                --sc-surface-2: #141414;
                --sc-border: #1e1e1e;
                --sc-text: #f2f2f2;
                --sc-muted: #8a8a8a;
                --sc-accent: #ff5500;
                --sc-accent-rgb: 255, 85, 0;
                --sc-accent-text: #ffffff;
                --sc-scroll-track: #000000;
                --sc-scroll-thumb: #2b2b2b;
            }
            html.sc-theme-light {
                color-scheme: light;
                --sc-bg: #ffffff;
                --sc-surface: #f4f4f6;
                --sc-surface-2: #e9e9ee;
                --sc-border: #dcdce0;
                --sc-text: #1a1a1d;
                --sc-muted: #66666e;
                --sc-accent: #ff5500;
                --sc-accent-rgb: 255, 85, 0;
                --sc-accent-text: #ffffff;
                --sc-scroll-track: #f4f4f6;
                --sc-scroll-thumb: #c4c4cc;
            }
            html.sc-theme-cyber {
                color-scheme: dark;
                --sc-bg: #0e0a18;
                --sc-surface: #151022;
                --sc-surface-2: #1f1733;
                --sc-border: #2d2246;
                --sc-text: #f0e9ff;
                --sc-muted: #9d8ec0;
                --sc-accent: #c05cff;
                --sc-accent-rgb: 192, 92, 255;
                --sc-accent-text: #ffffff;
                --sc-scroll-track: #0e0a18;
                --sc-scroll-thumb: #3a2b5c;
            }
            html.sc-theme-ocean {
                color-scheme: dark;
                --sc-bg: #091018;
                --sc-surface: #0e1722;
                --sc-surface-2: #152233;
                --sc-border: #1e3044;
                --sc-text: #e6f1fa;
                --sc-muted: #85a1ba;
                --sc-accent: #22b8cf;
                --sc-accent-rgb: 34, 184, 207;
                --sc-accent-text: #06131a;
                --sc-scroll-track: #091018;
                --sc-scroll-thumb: #263d56;
            }

            /* Map SoundCloud native CSS custom properties so buttons/toggles/icons follow theme */
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) body {
                --special-color: var(--sc-accent);
                --special-rgb: var(--sc-accent-rgb);
                --font-special-color: var(--sc-accent);
                --primary-color: var(--sc-accent);
                --secondary-color: var(--sc-surface-2);
                --surface-color: var(--sc-surface);
                --background-surface-color: var(--sc-bg);
                --highlight-color: var(--sc-surface-2);
                --background-highlight-color: var(--sc-surface-2);
                --font-primary-color: var(--sc-text);
                --font-secondary-color: var(--sc-muted);
                --link-color: var(--sc-accent);
                --font-link-color: var(--sc-accent);
                --button-special-background-color: var(--sc-accent);
                --button-special-font-color: var(--sc-accent-text);
                --button-special-border-color: var(--sc-accent);
                --button-primary-background-color: var(--sc-accent);
                --button-primary-font-color: var(--sc-accent-text);
                --button-primary-border-color: var(--sc-accent);
                --button-secondary-background-color: var(--sc-surface-2);
                --button-secondary-font-color: var(--sc-text);
                --button-secondary-border-color: var(--sc-border);
                --button-secondary-selected-background-color: var(--sc-accent);
                --button-secondary-selected-font-color: var(--sc-accent-text);
                --button-secondary-selected-border-color: var(--sc-accent);
                --button-tertiary-background-color: var(--sc-surface-2);
                --button-tertiary-font-color: var(--sc-text);
                --button-tertiary-selected-font-color: var(--sc-accent);
                --checkbox-checked-background-color: var(--sc-accent);
                --checkbox-checked-border-color: var(--sc-accent);
                --toggle-on-body-background-color: var(--sc-accent);
            }

            /* Scoped theme styles (shared across all non-original themes via CSS variables) */
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) body,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) #app,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) #content,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .l-container,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .l-content,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .l-main,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .l-fluid-fixed,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .stream__list {
                background-color: var(--sc-bg) !important;
                color: var(--sc-text) !important;
                border-color: var(--sc-border) !important;
            }

            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .soundList__item,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sound__body,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playControls,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playControls__inner,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playControls__bg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .header,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .header__inner,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .modal__modal,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .dropdownContent,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .commentForm__wrapper,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .listenEngagement,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .trackItem,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .compactTrackListItem,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sidebarModule {
                background-color: var(--sc-surface) !important;
                color: var(--sc-text) !important;
                border-color: var(--sc-border) !important;
            }

            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .g-tabs {
                background-color: transparent !important;
                border-color: var(--sc-border) !important;
                color: var(--sc-text) !important;
            }

            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) input,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) textarea {
                background-color: var(--sc-surface-2) !important;
                color: var(--sc-text) !important;
                border-color: var(--sc-border) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) input::placeholder,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) textarea::placeholder {
                color: var(--sc-muted) !important;
            }

            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .trackItem:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .compactTrackListItem:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .dropdownContent a:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .dropdownContent button:hover {
                background-color: var(--sc-surface-2) !important;
            }

            /* Borders */
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-border-light,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-border-light-top,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-border-light-bottom,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-border-light-left,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-border-light-right {
                border-color: var(--sc-border) !important;
            }

            /* Primary and muted text + links */
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) h1,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) h2,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) h3,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) h4,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-text,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-link-primary,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-link-dark,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .soundTitle__title,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .trackItem__trackTitle,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .compactTrackListItem__trackTitle,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackSoundBadge__titleLink {
                color: var(--sc-text) !important;
            }

            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-text-light,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-text-secondary,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-link-light,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-link-secondary,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .soundTitle__username,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .soundTitle__uploadTime,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .trackItem__username,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .compactTrackListItem__userName,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackSoundBadge__lightLink,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackTimeline__duration,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-ministats {
                color: var(--sc-muted) !important;
            }

            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) a {
                color: var(--sc-text);
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) a:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-link-primary:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-link-dark:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .soundTitle__title:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackSoundBadge__titleLink:hover {
                color: var(--sc-accent) !important;
            }

            /* Layout fix for bottom player bar & rounded buttons across all themes */
            .playControls,
            .playControls__inner,
            .playControls__wrapper {
                width: 100% !important;
                max-width: 100% !important;
                min-width: 0 !important;
                box-sizing: border-box !important;
                overflow: visible !important;
            }
            .playControls__inner {
                padding-left: 12px !important;
                padding-right: 12px !important;
            }
            .playControls__elements {
                width: 100% !important;
                min-width: 0 !important;
                display: flex !important;
                align-items: center !important;
                gap: 4px !important;
                overflow: visible !important;
                box-sizing: border-box !important;
            }
            .playControls__control,
            .playControls__volume,
            .skipControl,
            .playControl,
            .shuffleControl,
            .repeatControl,
            .volume__button {
                flex-shrink: 0 !important;
                overflow: visible !important;
                box-sizing: border-box !important;
            }
            .playControls__timeline {
                flex: 1 1 220px !important;
                min-width: 120px !important;
            }
            .playControls__soundBadge {
                width: auto !important;
                flex: 0 1 360px !important;
                min-width: 180px !important;
                max-width: 38vw !important;
                overflow: visible !important;
                box-sizing: border-box !important;
            }
            .playbackSoundBadge {
                width: 100% !important;
                min-width: 0 !important;
                display: flex !important;
                align-items: center !important;
                overflow: visible !important;
                box-sizing: border-box !important;
            }
            .playbackSoundBadge__titleContextContainer {
                flex: 1 1 80px !important;
                min-width: 60px !important;
                overflow: hidden !important;
            }
            .playbackSoundBadge__actions {
                display: inline-flex !important;
                align-items: center !important;
                flex-shrink: 0 !important;
                gap: 6px !important;
                margin-left: 8px !important;
                margin-right: 2px !important;
                overflow: visible !important;
            }
            .playbackSoundBadge__showQueue {
                width: 32px !important;
                min-width: 32px !important;
                height: 32px !important;
                padding: 0 !important;
                margin: 0 !important;
                display: inline-flex !important;
                align-items: center !important;
                justify-content: center !important;
                overflow: visible !important;
                box-sizing: border-box !important;
                flex-shrink: 0 !important;
                position: relative !important;
            }
            .playbackSoundBadge__showQueue svg,
            .playbackSoundBadge__queueIcon {
                width: 18px !important;
                height: 18px !important;
                min-width: 18px !important;
                min-height: 18px !important;
                padding: 0 !important;
                margin: 0 !important;
                display: block !important;
                position: static !important;
                transform: none !important;
                overflow: visible !important;
                flex-shrink: 0 !important;
            }
            .sc-button-play {
                box-sizing: border-box !important;
            }

            /* Buttons, playback controls, progress bars, and active navigation tabs */
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .skipControl,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .shuffleControl,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .volume__button,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackSoundBadge__showQueue {
                background-color: var(--sc-surface-2) !important;
                color: var(--sc-text) !important;
                border-color: var(--sc-border) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .skipControl svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .shuffleControl svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .volume__button svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackSoundBadge__showQueue svg {
                color: var(--sc-text) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .skipControl:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .shuffleControl:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .volume__button:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackSoundBadge__showQueue:hover {
                border-color: var(--sc-accent) !important;
                color: var(--sc-accent) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .shuffleControl.m-shuffling,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl.m-one,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl.m-all,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-like.sc-button-selected,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-repost.sc-button-selected,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-follow.sc-button-selected,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-active,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackSoundBadge__showQueue.m-active,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackSoundBadge__showQueue[aria-expanded='true'] {
                background-color: var(--sc-surface-2) !important;
                border-color: var(--sc-accent) !important;
                color: var(--sc-accent) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl.m-one .playControls__repeatIcon,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl.m-all .playControls__repeatIcon,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl.m-one svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl.m-all svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .shuffleControl.m-shuffling svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-like.sc-button-selected svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-repost.sc-button-selected svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackSoundBadge__showQueue.m-active svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackSoundBadge__showQueue[aria-expanded='true'] svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl.m-one svg *,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl.m-all svg *,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .shuffleControl.m-shuffling svg * {
                color: var(--sc-accent) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl.m-one svg [fill]:not([fill='none']),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl.m-all svg [fill]:not([fill='none']),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .shuffleControl.m-shuffling svg [fill]:not([fill='none']),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-like.sc-button-selected svg [fill]:not([fill='none']),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-repost.sc-button-selected svg [fill]:not([fill='none']),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackSoundBadge__showQueue.m-active svg [fill]:not([fill='none']),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackSoundBadge__showQueue[aria-expanded='true'] svg [fill]:not([fill='none']) {
                fill: var(--sc-accent) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl.m-one svg [stroke]:not([stroke='none']),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .repeatControl.m-all svg [stroke]:not([stroke='none']),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .shuffleControl.m-shuffling svg [stroke]:not([stroke='none']) {
                stroke: var(--sc-accent) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-selected:not(.sc-button-like):not(.sc-button-repost):not(.sc-button-follow),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-cta,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-primary,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-orange,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-play,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playControl,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playControl.playing {
                background-color: var(--sc-accent) !important;
                border-color: var(--sc-accent) !important;
                color: var(--sc-accent-text) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-selected:not(.sc-button-like):not(.sc-button-repost):not(.sc-button-follow) svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-cta svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-primary svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-play svg,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playControl svg {
                color: var(--sc-accent-text) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playControl svg [fill]:not([fill='none']),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-play svg [fill]:not([fill='none']),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-cta svg [fill]:not([fill='none']),
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-selected:not(.sc-button-like):not(.sc-button-repost):not(.sc-button-follow) svg [fill]:not([fill='none']) {
                fill: var(--sc-accent-text) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playControl:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-play:hover,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .sc-button-cta:hover {
                filter: brightness(1.1) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) svg [fill='#ff5500' i],
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) svg [fill='#f50' i] {
                fill: var(--sc-accent) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) svg [stroke='#ff5500' i],
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) svg [stroke='#f50' i] {
                stroke: var(--sc-accent) !important;
            }

            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackTimeline__progressBar,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .volume__sliderProgress,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackTimeline__progressHandle,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .volume__sliderHandle {
                background-color: var(--sc-accent) !important;
                border-color: var(--sc-accent) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackTimeline__progressBackground,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .volume__sliderBackground {
                background-color: var(--sc-border) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .playbackTimeline__timePassed {
                color: var(--sc-accent) !important;
            }

            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .g-tabs-link {
                color: var(--sc-muted) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .g-tabs-link:hover {
                color: var(--sc-text) !important;
            }
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .g-tabs-link.active,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .g-tabs-link[aria-selected='true'],
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .header__navMenu > li > a.selected,
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .header__navMenu > li > a[aria-current='page'],
            html:is(.sc-theme-dark, .sc-theme-amoled, .sc-theme-light, .sc-theme-cyber, .sc-theme-ocean) .header__navMenu > li > a.active {
                color: var(--sc-accent) !important;
                border-color: var(--sc-accent) !important;
            }

            /* Optional clean mode & ad-blocker: hide ads, promoted posts, and promo/upsell banners */
            html.sc-desktop-clean .header__goUpsell,
            html.sc-desktop-clean .header__goUpsell_link,
            html.sc-desktop-clean .header__upsell,
            html.sc-desktop-clean .sidebarModule__upsell,
            html.sc-desktop-clean .l-product-banners,
            html.sc-desktop-clean .announcements,
            html.sc-desktop-clean .mobileAppUpsell,
            html.sc-desktop-clean [class*='UpsellBanner'],
            html.sc-desktop-clean [class*='upsellBanner'],
            html.sc-desktop-clean [class*='upsell' i],
            html.sc-desktop-clean [class*='GoBanner'],
            html.sc-desktop-clean [data-testid*='upsell' i],
            html.sc-desktop-clean [data-testid*='ad-' i],
            html.sc-desktop-clean [data-testid*='promoted' i],
            html.sc-desktop-clean .adBanner,
            html.sc-desktop-clean .visualSound__ad,
            html.sc-desktop-clean .audioAd,
            html.sc-desktop-clean .displayAd,
            html.sc-desktop-clean .promotedTrack,
            html.sc-desktop-clean .promotedSound,
            html.sc-desktop-clean .spotlight__promoted,
            html.sc-desktop-clean [class*='AdSlot'],
            html.sc-desktop-clean [class*='adSlot'],
            html.sc-desktop-clean [class*='Promoted'],
            html.sc-desktop-clean [id*='google_ads'],
            html.sc-desktop-clean iframe[src*='doubleclick'],
            html.sc-desktop-clean iframe[src*='googlesyndication'],
            html.sc-desktop-clean iframe[src*='adswizz'],
            html.sc-desktop-clean .sc-desktop-ad-hidden {
                display: none !important;
            }

            /* Mini-Player mode */
            html.sc-desktop-mini body {
                overflow: hidden !important;
                background: var(--sc-bg, #141414) !important;
            }
            html.sc-desktop-mini .header,
            html.sc-desktop-mini #content,
            html.sc-desktop-mini .announcements {
                visibility: hidden !important;
                pointer-events: none !important;
            }
            html.sc-desktop-mini .playControls {
                position: fixed !important;
                top: 0 !important;
                left: 0 !important;
                right: 0 !important;
                bottom: 0 !important;
                height: 100vh !important;
                width: 100vw !important;
                z-index: 999999 !important;
                background: var(--sc-surface, #181818) !important;
                border-top: none !important;
                display: flex !important;
                align-items: center !important;
                justify-content: center !important;
                padding: 0 12px !important;
                box-sizing: border-box !important;
            }
            html.sc-desktop-mini .playControls__inner {
                width: 100% !important;
                max-width: 100% !important;
                height: auto !important;
            }
            html.sc-desktop-mini .playControls__wrapper {
                width: 100% !important;
                min-width: 0 !important;
                padding: 0 !important;
                display: flex !important;
                flex-wrap: wrap !important;
                align-items: center !important;
                justify-content: space-between !important;
                gap: 6px !important;
            }
            html.sc-desktop-mini .playControls__soundBadge {
                width: 100% !important;
                min-width: 0 !important;
                order: -1 !important;
                margin-bottom: 4px !important;
            }
            html.sc-desktop-mini .playControls__timeline {
                flex: 1 1 200px !important;
                min-width: 120px !important;
                margin: 0 8px !important;
            }
        `;
        (document.head || document.documentElement).appendChild(style);
    }

    function sendMsg(payload) {
        try {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify(payload));
            }
        } catch (_) {}
    }

    function getTrackState() {
        const playBtn = document.querySelector('.playControl');
        const isPlaying = !!(playBtn && playBtn.classList.contains('playing'));
        const titleEl = document.querySelector('.playbackSoundBadge__titleLink');
        const artistEl = document.querySelector('.playbackSoundBadge__lightLink');

        let title = '';
        let artist = '';
        let trackUrl = '';

        if (navigator.mediaSession && navigator.mediaSession.metadata) {
            title = navigator.mediaSession.metadata.title || '';
            artist = navigator.mediaSession.metadata.artist || '';
        }

        if (!title && titleEl) {
            title = (titleEl.getAttribute('title') || titleEl.textContent || '').trim();
        }
        if (!artist && artistEl) {
            artist = (artistEl.getAttribute('title') || artistEl.textContent || '').trim();
        }
        if (titleEl && titleEl.href) {
            trackUrl = titleEl.href;
        }

        return {
            type: 'playerState',
            playing: isPlaying,
            title: title,
            artist: artist,
            trackUrl: trackUrl,
            pageUrl: location.href,
            canGoBack: history.length > 1
        };
    }

    // Layer 2 Ad-Blocker & Legitimate Transcoding Quality Preference
    const AD_URL_RE = /(?:doubleclick\.net|googlesyndication\.com|googleadservices\.com|googletagservices\.com|2mdn\.net|imasdk\.googleapis\.com|adswizz\.com|ads\.soundcloud\.com|audio-ads\.soundcloud\.com|ad-delivery\.soundcloud\.com|ads-api\.soundcloud\.com|promoted\.soundcloud\.com|eventlogger\.soundcloud\.com|moatads\.com|adsafeprotected\.com|doubleverify\.com|scorecardresearch\.com|quantserve\.com|adsrvr\.org|adnxs\.com|rubiconproject\.com|pubmatic\.com|openx\.net|criteo\.|taboola\.|outbrain\.|amazon-adsystem\.|innovid\.com|serving-sys\.com|flashtalking\.com|\/(?:audio-ads|video-ads|visual-ads|ads|vast|promoted|commercials|monetization\/(?:ads|offers)|me\/upsell|upsells?|sponsored)(?:[/?#]|$))/i;

    function isAdEntry(item) {
        if (!item || typeof item !== 'object') return false;
        if (item.ad_urn || item.is_promoted || item.promoted_by || item.sponsor || item.is_ad || item.ad_id || item.sponsored || item.promotion) return true;
        if (item.urn && typeof item.urn === 'string' && /^soundcloud:ads:/i.test(item.urn)) return true;
        if (item.type === 'ad' || item.kind === 'ad' || item.kind === 'promoted' || item.type === 'promoted' || item.type === 'promoted-track' || item.type === 'upsell') return true;
        if (item.ad && typeof item.ad === 'object') return true;
        if (item.track && typeof item.track === 'object' && (item.track.ad_urn || item.track.is_promoted || item.track.promoted_by || item.track.is_ad || item.track.ad_id || item.track.sponsored || item.track.kind === 'ad')) return true;
        return false;
    }

    // Legitimately reorder non-paywalled transcodings already granted by the server (never spoofs subscription or unlocks Go+ streams).
    function scoreTranscoding(t, mode) {
        if (!t || typeof t !== 'object') return 0;
        if (t.snipped === true) return -100;
        const fmt = t.format || {};
        const mime = (fmt.mime_type || '').toLowerCase();
        const proto = (fmt.protocol || '').toLowerCase();
        const preset = (t.preset || '').toLowerCase();
        const isOpus = mime.indexOf('opus') !== -1 || preset.indexOf('opus') === 0;
        const isMp3 = mime.indexOf('mpeg') !== -1 || preset.indexOf('mp3') === 0;
        const isProg = proto === 'progressive';
        if (mode === 'mp3') {
            if (isMp3 && isProg) return 30;
            if (isMp3) return 20;
            if (isOpus) return 10;
            return 5;
        }
        // 'high' mode: prefer Opus (higher perceptual quality at standard tier), then progressive/HLS MP3
        if (isOpus) return 30;
        if (isMp3 && isProg) return 20;
        if (isMp3) return 10;
        return 5;
    }

    function reorderTranscodings(obj) {
        const mode = window.__scDesktopAudioQuality || 'high';
        if (mode === 'auto' || !obj || !obj.media || !Array.isArray(obj.media.transcodings) || obj.media.transcodings.length < 2) return;
        obj.media.transcodings.sort(function (a, b) {
            return scoreTranscoding(b, mode) - scoreTranscoding(a, mode);
        });
    }

    function sanitizePayload(data, depth) {
        if (!data || typeof data !== 'object' || (depth || 0) > 8) return data;
        const nextDepth = (depth || 0) + 1;
        if (Array.isArray(data)) {
            const out = [];
            for (let i = 0; i < data.length; i++) {
                if (!window.__scDesktopAdBlock || !isAdEntry(data[i])) {
                    out.push(sanitizePayload(data[i], nextDepth));
                }
            }
            return out;
        }
        reorderTranscodings(data);
        if (window.__scDesktopAdBlock) {
            if (Array.isArray(data.ads)) data.ads = [];
            if (Array.isArray(data.promoted_tracks)) data.promoted_tracks = [];
        }
        const arrayKeys = ['collection', 'sections', 'items', 'cards', 'tracks'];
        for (let k = 0; k < arrayKeys.length; k++) {
            const key = arrayKeys[k];
            if (Array.isArray(data[key])) {
                const filtered = [];
                for (let i = 0; i < data[key].length; i++) {
                    if (!window.__scDesktopAdBlock || !isAdEntry(data[key][i])) {
                        filtered.push(sanitizePayload(data[key][i], nextDepth));
                    }
                }
                data[key] = filtered;
            }
        }
        if (data.track && typeof data.track === 'object') {
            reorderTranscodings(data.track);
        }
        return data;
    }

    function installNetworkAdFilter() {
        if (window.__scDesktopNetFilter) return;
        window.__scDesktopNetFilter = true;

        if (typeof window.fetch === 'function') {
            const origFetch = window.fetch.bind(window);
            window.fetch = function (input, init) {
                const url = typeof input === 'string' ? input : (input && input.url ? input.url : '');
                if (window.__scDesktopAdBlock && url && AD_URL_RE.test(url)) {
                    return Promise.resolve(new Response(JSON.stringify({ collection: [], ads: [], next_href: null }), {
                        status: 200,
                        headers: { 'Content-Type': 'application/json' }
                    }));
                }
                return origFetch(input, init).then(function (resp) {
                    if (!resp || !resp.ok || !url || (url.indexOf('api-v2.soundcloud.com') === -1 && url.indexOf('api.soundcloud.com') === -1)) {
                        return resp;
                    }
                    const shouldClean = window.__scDesktopAdBlock || (window.__scDesktopAudioQuality && window.__scDesktopAudioQuality !== 'auto');
                    if (!shouldClean) return resp;
                    const ct = (resp.headers && resp.headers.get('content-type')) || '';
                    if (ct.indexOf('application/json') === -1) return resp;
                    return resp.clone().json().then(function (json) {
                        const clean = sanitizePayload(json, 0);
                        return new Response(JSON.stringify(clean), {
                            status: resp.status,
                            statusText: resp.statusText,
                            headers: resp.headers
                        });
                    }).catch(function () {
                        return resp;
                    });
                });
            };
        }

        if (window.XMLHttpRequest && window.XMLHttpRequest.prototype) {
            const origOpen = window.XMLHttpRequest.prototype.open;
            const origSend = window.XMLHttpRequest.prototype.send;
            window.XMLHttpRequest.prototype.open = function (method, url) {
                this.__scUrl = typeof url === 'string' ? url : '';
                return origOpen.apply(this, arguments);
            };
            window.XMLHttpRequest.prototype.send = function () {
                const xhr = this;
                const url = xhr.__scUrl || '';
                if (window.__scDesktopAdBlock && url && AD_URL_RE.test(url)) {
                    try {
                        Object.defineProperty(xhr, 'readyState', { configurable: true, get: function () { return 4; } });
                        Object.defineProperty(xhr, 'status', { configurable: true, get: function () { return 200; } });
                        Object.defineProperty(xhr, 'responseText', { configurable: true, get: function () { return '{""collection"":[],""ads"":[],""next_href"":null}'; } });
                        Object.defineProperty(xhr, 'response', { configurable: true, get: function () { return '{""collection"":[],""ads"":[],""next_href"":null}'; } });
                        setTimeout(function () {
                            if (typeof xhr.onreadystatechange === 'function') xhr.onreadystatechange();
                            if (typeof xhr.onload === 'function') xhr.onload();
                        }, 0);
                    } catch (_) {}
                    return;
                }
                if (url && (url.indexOf('api-v2.soundcloud.com') !== -1 || url.indexOf('api.soundcloud.com') !== -1)) {
                    xhr.addEventListener('readystatechange', function () {
                        const shouldClean = window.__scDesktopAdBlock || (window.__scDesktopAudioQuality && window.__scDesktopAudioQuality !== 'auto');
                        if (!shouldClean || xhr.readyState !== 4 || xhr.status !== 200) return;
                        try {
                            const raw = xhr.responseText;
                            if (!raw || raw.charAt(0) !== '{') return;
                            const parsed = JSON.parse(raw);
                            const clean = JSON.stringify(sanitizePayload(parsed, 0));
                            Object.defineProperty(xhr, 'responseText', { configurable: true, get: function () { return clean; } });
                            Object.defineProperty(xhr, 'response', { configurable: true, get: function () { return xhr.responseType === 'json' ? JSON.parse(clean) : clean; } });
                        } catch (_) {}
                    });
                }
                return origSend.apply(this, arguments);
            };
        }
    }

    installNetworkAdFilter();

    let savedVolumes = new WeakMap();
    function scrubAdsFromDomAndAudio() {
        if (!window.__scDesktopAdBlock) return;
        const selectors = [
            '.header__goUpsell',
            '.header__upsell',
            '.sidebarModule__upsell',
            '.l-product-banners',
            '.adBanner',
            '.visualSound__ad',
            '.audioAd',
            '.displayAd',
            '.promotedTrack',
            '.promotedSound',
            '.spotlight__promoted',
            '.mobileAppUpsell',
            '[class*=\'UpsellBanner\']',
            '[class*=\'upsellBanner\']',
            '[class*=\'AdSlot\']',
            '[class*=\'adSlot\']',
            '[data-testid*=\'upsell\' i]',
            '[data-testid*=\'ad-\' i]',
            '[data-testid*=\'promoted\' i]'
        ];
        const nodes = document.querySelectorAll(selectors.join(','));
        for (let i = 0; i < nodes.length; i++) {
            nodes[i].classList.add('sc-desktop-ad-hidden');
        }

        const cards = document.querySelectorAll('.soundList__item, .searchList__item, .stream__item, .compactTrackList__item, .playableTile, .carouselItem');
        for (let i = 0; i < cards.length; i++) {
            const card = cards[i];
            if (card.classList.contains('sc-desktop-ad-hidden')) continue;
            if (card.querySelector('.promotedTrack, .promotedSound, .soundContext__promoted, [class*=\'promoted\' i], [class*=\'AdSlot\'], [data-testid*=\'promoted\' i]')) {
                card.classList.add('sc-desktop-ad-hidden');
            }
        }

        const adPlaying = !!document.querySelector('.playControls__ad, .playbackSoundBadge__ad, .audioAd.isPlaying, .visualSound__ad, .playControls.is-ad, .playbackSoundBadge.is-ad');
        const media = document.querySelectorAll('audio, video');
        for (let i = 0; i < media.length; i++) {
            const m = media[i];
            if (adPlaying) {
                if (!savedVolumes.has(m)) {
                    savedVolumes.set(m, { muted: m.muted, volume: m.volume });
                }
                m.muted = true;
                if (isFinite(m.duration) && m.duration > 0.2 && m.currentTime < m.duration - 0.15) {
                    try { m.currentTime = m.duration - 0.05; } catch (_) {}
                }
            } else if (savedVolumes.has(m)) {
                const prev = savedVolumes.get(m);
                savedVolumes.delete(m);
                m.muted = prev.muted;
                m.volume = prev.volume;
            }
        }
        if (adPlaying) {
            const nextBtn = document.querySelector('.skipControl__next:not(.disabled)');
            if (nextBtn) nextBtn.click();
        }
    }

    let lastSig = '';
    function pushStateIfChanged(force) {
        ensureStyle();
        scrubAdsFromDomAndAudio();
        const st = getTrackState();
        const sig = [st.playing, st.title, st.artist, st.trackUrl, st.pageUrl].join('|');
        if (force || sig !== lastSig) {
            lastSig = sig;
            sendMsg(st);
        }
    }

    window.__scDesktop = {
        togglePlay: function () {
            const btn = document.querySelector('.playControl');
            if (btn) btn.click();
            setTimeout(function () { pushStateIfChanged(true); }, 120);
        },
        next: function () {
            const btn = document.querySelector('.skipControl__next');
            if (btn) btn.click();
            setTimeout(function () { pushStateIfChanged(true); }, 180);
        },
        prev: function () {
            const btn = document.querySelector('.skipControl__previous');
            if (btn) btn.click();
            setTimeout(function () { pushStateIfChanged(true); }, 180);
        },
        focusSearch: function () {
            const input = document.querySelector('.headerSearch__input, input[type=""search""]');
            if (input) {
                input.focus();
                input.select();
            }
        },
        setCleanMode: function (enabled) {
            ensureStyle();
            document.documentElement.classList.toggle('sc-desktop-clean', !!enabled);
        },
        setAdBlock: function (enabled) {
            window.__scDesktopAdBlock = !!enabled;
            ensureStyle();
            document.documentElement.classList.toggle('sc-desktop-clean', !!enabled);
            if (enabled) scrubAdsFromDomAndAudio();
        },
        setMiniMode: function (enabled) {
            ensureStyle();
            document.documentElement.classList.toggle('sc-desktop-mini', !!enabled);
        },
        setTheme: function (themeId) {
            applyThemeClass(themeId);
            ensureStyle();
        },
        setAudioQuality: function (mode) {
            window.__scDesktopAudioQuality = (mode === 'auto' || mode === 'mp3' || mode === 'high') ? mode : 'high';
        },
        getTrackUrl: function () {
            const st = getTrackState();
            return st.trackUrl || location.href;
        }
    };

    // Mouse Back / Forward hardware buttons
    window.addEventListener('mouseup', function (e) {
        if (e.button === 3) {
            e.preventDefault();
            sendMsg({ type: 'nav', action: 'back' });
        } else if (e.button === 4) {
            e.preventDefault();
            sendMsg({ type: 'nav', action: 'forward' });
        }
    }, true);

    // Keyboard shortcuts routed reliably to native host or search bar
    window.addEventListener('keydown', function (e) {
        if ((e.ctrlKey || e.metaKey) && !e.shiftKey && !e.altKey) {
            const k = (e.key || '').toLowerCase();
            if (k === 'k' || k === 'l') {
                e.preventDefault();
                window.__scDesktop.focusSearch();
            } else if (k === 'm') {
                e.preventDefault();
                sendMsg({ type: 'shortcut', action: 'toggleMini' });
            } else if (k === 'h') {
                e.preventDefault();
                sendMsg({ type: 'shortcut', action: 'toggleToolbar' });
            }
        } else if (e.key === 'F11') {
            e.preventDefault();
            sendMsg({ type: 'shortcut', action: 'toggleFullscreen' });
        }
    }, true);

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            ensureStyle();
            pushStateIfChanged(true);
        });
    } else {
        ensureStyle();
        pushStateIfChanged(true);
    }

    setInterval(function () { pushStateIfChanged(false); }, 900);
})();
";
    }
}
