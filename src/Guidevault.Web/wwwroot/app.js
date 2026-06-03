const state = {
  items: [], filtered: [], selected: null, filter: 'All Content', categoryFilter: '', viewMode: 'all', activeTab: 'overview', customFilter: null,
  reader: { item: null, pages: [], index: 0, animating: false, displayMode: 2, transitionMode: 'stable', overlayVisible: false, advancedVisible: false, magnifierSettingsVisible: false, scrubbing: false, shading: null, zoom: 100, magnifier: null, magnifierActive: false, longPressTimer: null, suppressHitClickUntil: 0, backgrounds: [], background: '', backgroundBrightness: 72 },
  libraryPath: '',
  libraries: [],
  editingLibraryIndex: null,
  iconMap: {},
  collapsedCategoryGroups: { 'Manual': true, 'Strategy Guide': true, 'Magazine': true, 'Any': true, 'Publisher': true, 'Decade': true },
  categoryStructure: 'content-type',
  coverScale: 100,
  tasks: [],
  taskPanelVisible: false,
  taskPollTimer: null,
  libraryLoadedOnce: false,
  auth: { profile: null, authenticated: false, editing: false, appStarted: false },
  readingProfiles: { presets: {}, defaultPresetId: 'default', groupAssignments: {}, entryAssignments: {} },
  opds: { connectionUrl: '', selectedKeyId: '', keys: [], editingUrl: false, revealUrl: false, creatingKey: false },
  devices: { emailDevices: [], clientDevices: [], generatedAt: null, addingEmail: false, editingEmailId: '', editingClientId: '', clientMenuId: '' },
  metadataManager: { selectedIds: [], dirty: {}, filterKind: '', search: '', missing: '', category: '', visibleColumns: [] },
  keybinds: { bindings: {}, awaitingId: '' },
  folderBrowser: { targetInputId: '', currentPath: '/app/data/library', roots: [] },
  customize: { activeTab: 'home', homeShelves: [], sideNav: { customItems: [] } },
  serverSettings: null,
  emailSettings: null,
  emailHistory: [],
  usersSettings: { users: [], libraries: [], permissions: [] },
  taskSettings: null,
  homeShelfOffsets: {},
  statistics: { activeTab: 'stats', range: 'all' },
  profilePage: { activeTab: 'overview', range: 'all' },
  preferences: { useColorscape: false },
  colorscape: { itemId: '', token: 0, cache: {} },
  systemInfo: null,
  performanceInfo: null,
  updateCheck: null,
  updateCheckTimer: null,
  deviceHeartbeatTimer: null
};
const $ = id => document.getElementById(id);
function setText(id, value) {
  const el = $(id);
  if (el) el.textContent = value || '—';
}
const READER_BOOKMARKS_KEY = 'guidevault.readerBookmarks.v1';
const READER_SHADING_KEY = 'guidevault.readerShading.v1';
const READER_MAGNIFIER_KEY = 'guidevault.readerMagnifier.v1';
const READER_BACKGROUND_KEY = 'guidevault.readerBackground.v1';
const READER_BACKGROUND_BRIGHTNESS_KEY = 'guidevault.readerBackgroundBrightness.v1';
const GUIDEVAULT_METADATA_OVERRIDES_KEY = 'guidevault.metadataOverrides.v2';
const GUIDEVAULT_METADATA_COLUMNS_KEY = 'guidevault.metadataManagerColumns.v1';
const GUIDEVAULT_LOGIN_PROFILE_KEY = 'guidevault.localLoginProfile.v1';
const GUIDEVAULT_READING_PROFILES_KEY = 'guidevault.readingProfiles.v1';
const GUIDEVAULT_OPDS_SETTINGS_KEY = 'guidevault.opdsSettings.v1';
const GUIDEVAULT_PREFERENCES_KEY = 'guidevault.preferences.v1';
const GUIDEVAULT_KEYBINDS_KEY = 'guidevault.keybinds.v1';
const GUIDEVAULT_CUSTOMIZE_KEY = 'guidevault.customize.v1';
const GUIDEVAULT_READING_ACTIVITY_KEY = 'guidevault.readingActivity.v1';
const GUIDEVAULT_CATEGORY_STRUCTURE_KEY = 'guidevault.categoryStructure.v1';
const GUIDEVAULT_COVER_SIZE_KEY = 'guidevault.libraryCoverSize.v1';
const GUIDEVAULT_FAVORITES_KEY = 'guidevault.favorites.v1';
const GUIDEVAULT_APP_VERSION = '0.9.40';

const EMAIL_TEMPLATE_PRESETS = [
  {
    id: 'guidevault-default',
    name: 'Guidevault Invite',
    subject: 'Guidevault invite',
    body: '<h2>You have been invited to {{appName}}</h2><p>Hello {{displayName}},</p><p>You can open your Guidevault library here:</p><p><a href="{{inviteUrl}}">{{inviteUrl}}</a></p><p>Permissions: {{permissions}}</p>'
  },
  {
    id: 'clean-access',
    name: 'Clean Access Notice',
    subject: 'Your Guidevault access is ready',
    body: '<div style="font-family:Arial,sans-serif;line-height:1.55"><h2>Guidevault access is ready</h2><p>Hi {{displayName}},</p><p>Your account has been invited to Guidevault.</p><p><strong>Open:</strong> <a href="{{inviteUrl}}">{{inviteUrl}}</a></p><p><strong>Permissions:</strong> {{permissions}}</p></div>'
  },
  {
    id: 'plain-text',
    name: 'Plain Text Invite',
    subject: 'Guidevault invite',
    body: 'Hello {{displayName}},\n\nYou have been invited to {{appName}}.\n\nOpen: {{inviteUrl}}\n\nPermissions: {{permissions}}'
  }
];

const EMAIL_PROVIDER_PRESETS = {
  'transactional-resend': {
    label: 'Resend API',
    summary: 'Transactional API',
    endpoint: 'https://api.resend.com/emails',
    help: 'Recommended for a public Guidevault instance. Verify your sending domain in Resend, paste a send-only API key, and use a sender address on that verified domain.'
  },
  'transactional-sendgrid': {
    label: 'SendGrid API',
    summary: 'Transactional API',
    endpoint: 'https://api.sendgrid.com/v3/mail/send',
    help: 'Use a SendGrid API key with Mail Send permission and a verified sender/domain. This avoids personal-mailbox basic-auth problems.'
  },
  smtp: {
    label: 'Generic SMTP / App Password',
    summary: 'Legacy SMTP',
    endpoint: '',
    help: 'Use only with providers that support SMTP app passwords or service credentials. Outlook.com password SMTP will generally fail because it expects modern auth.'
  }
};
const GUIDEVAULT_DEVICE_HEARTBEAT_MS = 120000;
const GUIDEVAULT_UPDATE_CHECK_MS = 30 * 60 * 1000;
const fmtBytes = n => n > 1024 ** 3 ? `${(n / 1024 ** 3).toFixed(1)} GB` : `${(n / 1024 ** 2).toFixed(1)} MB`;
const categoryOf = item => item.category || item.system || 'Unsorted';
const associatedPlatformsOf = item => Array.isArray(item?.associatedPlatforms)
  ? item.associatedPlatforms.map(p => String(p || '').trim()).filter(Boolean)
  : String(item?.associatedPlatforms || '').split(',').map(p => p.trim()).filter(Boolean);
const platformListText = item => associatedPlatformsOf(item).join(', ');
const MULTI_PLATFORM_LABEL = 'Multi-Platform';
function platformNamesEqual(a, b) {
  return String(a || '').trim().localeCompare(String(b || '').trim(), undefined, { sensitivity: 'accent' }) === 0;
}
function hasMultipleAssociatedPlatforms(itemOrPlatforms) {
  const platforms = Array.isArray(itemOrPlatforms) ? itemOrPlatforms : associatedPlatformsOf(itemOrPlatforms);
  const unique = [];
  (platforms || []).forEach(platform => pushUniquePlatformBucket(unique, platform));
  return unique.length > 1;
}
function preferredPlatformOf(item) {
  if (item?.kind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(item)) return MULTI_PLATFORM_LABEL;
  return item?.category || item?.system || item?.primarySystem || '';
}
function activeLibraryPlatformForItem(item) {
  if (!item || !state.categoryFilter) return '';
  const parts = String(state.categoryFilter).split('::');
  const kind = parts.shift() || '';
  const category = parts.join('::');
  if (kind && item.kind !== kind) return '';
  if (!category) return '';
  return libraryCategoryKeysForItem(item).some(value => platformNamesEqual(value, category)) ? category : '';
}
function detailSystemLabelForItem(item) {
  if (!item) return '—';
  const currentLibrary = activeLibraryPlatformForItem(item);
  if (currentLibrary) return currentLibrary;
  if (item.kind === 'Strategy Guide') {
    const platforms = associatedPlatformsOf(item);
    if (hasMultipleAssociatedPlatforms(platforms)) return MULTI_PLATFORM_LABEL;
    if (platforms.length === 1) return platforms[0];
  }
  return detectedSystemOf(item);
}
function platformInitials(name) {
  const text = String(name || '').trim();
  if (!text) return '?';
  const known = {
    'Nintendo Entertainment System': 'NES',
    'Super Nintendo Entertainment System': 'SNES',
    'Nintendo GameCube': 'GC',
    'Nintendo 64': 'N64',
    'Nintendo Switch': 'NS',
    'PlayStation': 'PS',
    'Sony PlayStation': 'PS',
    'PlayStation 2': 'PS2',
    'PlayStation 3': 'PS3',
    'PlayStation 4': 'PS4',
    'PlayStation 5': 'PS5',
    'PlayStation Portable': 'PSP',
    'Sega Genesis': 'GEN',
    'Sega Dreamcast': 'DC',
    'Xbox': 'XB',
    'Xbox 360': '360',
    'Xbox One': 'X1'
  };
  if (known[text]) return known[text];
  const words = text.split(/\s+/).filter(Boolean);
  if (words.length === 1) return words[0].slice(0, 3).toUpperCase();
  return words.slice(0, 3).map(word => word[0]).join('').toUpperCase();
}
function platformIconBadgeHtml(name) {
  const label = String(name || '').trim();
  const icon = platformIconHtml(label, 'platform-icon tiny');
  return `<span class="card-platform-icon-badge" title="${escapeForAttribute(label)}" aria-label="${escapeForAttribute(label)}">${icon || `<span>${escapeHtml(platformInitials(label))}</span>`}</span>`;
}
function libraryCardPlatformMetaHtml(item) {
  const platforms = associatedPlatformsOf(item);
  if (item?.kind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(platforms)) {
    return `<div class="card-platform-stack">
      <div class="card-platform-icons" title="${escapeForAttribute(platforms.join(', '))}">${platforms.map(platformIconBadgeHtml).join('')}</div>
      <small class="card-category card-category-multi"><span>${escapeHtml(MULTI_PLATFORM_LABEL)}</span></small>
    </div>`;
  }
  const category = categoryOf(item);
  return `<small class="card-category">${platformIconHtml(category, 'platform-icon tiny')}<span>${escapeHtml(category)}${hasSequence(item) ? ` • #${escapeHtml(item.issueNumber)}` : ''}</span></small>`;
}
function isMultiPlatformBucketName(value) {
  return /^multi[-\s]*platform(?: strategy guides?)?$/i.test(String(value || '').trim());
}
function pushUniquePlatformBucket(values, value) {
  const text = String(value || '').trim();
  if (!text || text === '—' || /^unknown$/i.test(text) || isMultiPlatformBucketName(text)) return;
  if (!values.some(existing => existing.localeCompare(text, undefined, { sensitivity: 'accent' }) === 0)) values.push(text);
}
function libraryCategoryKeysForItem(item) {
  const values = [];
  if (!item) return ['Unsorted'];
  const preferred = item.category || item.system || item.primarySystem || '';
  if (item.kind === 'Strategy Guide') {
    associatedPlatformsOf(item).forEach(platform => pushUniquePlatformBucket(values, platform));
    if (!values.length) pushUniquePlatformBucket(values, preferred);
    return values.length ? values : ['Unsorted Strategy Guides'];
  }
  pushUniquePlatformBucket(values, preferred);
  if (!values.length && item.kind === 'Magazine') pushUniquePlatformBucket(values, item.magazineTitle || item.series || '');
  return values.length ? values : ['Unsorted'];
}
function itemMatchesCategoryFilter(item, categoryFilter = state.categoryFilter) {
  if (!categoryFilter) return true;
  const parts = String(categoryFilter).split('::');
  const filterKind = parts.shift() || '';
  const category = parts.join('::');
  if (!category) return true;
  if (filterKind === 'Publisher') return String(item?.publisher || 'Unsorted Publisher').trim().localeCompare(category, undefined, { sensitivity: 'accent' }) === 0;
  if (filterKind === 'Decade') return decadeLabelForItem(item).localeCompare(category, undefined, { sensitivity: 'accent' }) === 0;
  if (filterKind && filterKind !== 'Any' && item?.kind !== filterKind) return false;
  return libraryCategoryKeysForItem(item).some(value => value.localeCompare(category, undefined, { sensitivity: 'accent' }) === 0);
}
function decadeLabelForItem(item) {
  const year = Number.parseInt(String(item?.year || item?.coverDate || item?.releaseDate || '').match(/\d{4}/)?.[0] || '', 10);
  if (!Number.isFinite(year) || year <= 0) return 'Unknown Decade';
  return `${Math.floor(year / 10) * 10}s`;
}
const displayTitle = item => item.series && item.kind === 'Magazine' && item.issueNumber ? `${item.series} #${item.issueNumber}` : item.title;
const hasSequence = item => item.kind === 'Magazine' && !!String(item.issueNumber || '').trim();
const issueValue = item => Number.parseFloat(String(item.issueNumber || '').replace(/[^0-9.]/g, '')) || 0;


function defaultReadingProfile() {
  return {
    displayMode: 2,
    transitionMode: 'stable',
    background: '',
    zoom: 100
  };
}

function defaultReadingProfilePreset() {
  return {
    id: 'default',
    name: 'Default',
    ...defaultReadingProfile(),
    builtIn: true,
    updatedAt: null
  };
}

function emptyReadingProfilesState() {
  return {
    presets: { default: defaultReadingProfilePreset() },
    defaultPresetId: 'default',
    groupAssignments: {},
    entryAssignments: {}
  };
}

function currentReaderSettingsAsProfile() {
  return normalizeReadingProfile({
    displayMode: state.reader.displayMode,
    transitionMode: state.reader.transitionMode,
    background: state.reader.background,
    zoom: state.reader.zoom
  });
}

function normalizeReaderTransitionMode(mode) {
  const allowed = new Set(['stable', 'fade', 'dissolve', 'slide', 'push', 'page']);
  return allowed.has(String(mode || '')) ? String(mode) : 'stable';
}

function normalizeReadingProfile(value = {}) {
  const defaults = defaultReadingProfile();
  return {
    displayMode: normalizeReaderDisplayMode(value.displayMode ?? value.pageTypeDisplay ?? defaults.displayMode),
    transitionMode: normalizeReaderTransitionMode(value.transitionMode ?? value.transitionType ?? defaults.transitionMode),
    background: String((value.background ?? value.backgroundType ?? defaults.background) || '').trim(),
    zoom: clampNumber(value.zoom ?? value.zoomLevel ?? defaults.zoom, 70, 145, defaults.zoom),
    label: String(value.label || '').trim(),
    targetType: String(value.targetType || '').trim(),
    updatedAt: value.updatedAt || null
  };
}

function readingProfileSlug(value) {
  return String(value || '').trim().toLowerCase().replace(/\s+/g, ' ').replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'profile';
}

function normalizeReadingProfileKeyPart(value) {
  return String(value || '').trim().toLowerCase().replace(/\s+/g, ' ').replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'unsorted';
}

function readingProfileFriendlyKey(key) {
  const raw = String(key || '').replace(/^[^:]+:/, '').replace(/[-_]+/g, ' ').trim();
  return raw ? raw.replace(/\b\w/g, c => c.toUpperCase()) : 'Reading Profile';
}

function normalizeReadingProfilePreset(value = {}, fallbackId = '') {
  const base = normalizeReadingProfile(value);
  const id = String(value.id || fallbackId || readingProfileSlug(value.name || value.label || 'profile')).trim() || 'profile';
  const name = String(value.name || value.label || readingProfileFriendlyKey(id)).trim() || 'Reading Profile';
  return {
    id,
    name,
    displayMode: base.displayMode,
    transitionMode: base.transitionMode,
    background: base.background,
    zoom: base.zoom,
    builtIn: id === 'default' || !!value.builtIn,
    updatedAt: value.updatedAt || null
  };
}

function makeReadingProfilePresetId(name, existing = {}) {
  const base = readingProfileSlug(name || 'custom-profile');
  let id = `profile-${base}`;
  let index = 2;
  while (existing[id]) {
    id = `profile-${base}-${index}`;
    index += 1;
  }
  return id;
}

function normalizeReadingProfiles(value = {}) {
  const normalized = emptyReadingProfilesState();
  const source = value || {};

  if (source.presets && typeof source.presets === 'object') {
    normalized.presets = {};
    Object.entries(source.presets || {}).forEach(([id, profile]) => {
      if (!id) return;
      const preset = normalizeReadingProfilePreset({ ...(profile || {}), id: (profile && profile.id) || id }, id);
      normalized.presets[preset.id] = preset;
    });
  } else {
    // Migrate the first scaffold, which stored full profiles directly on group/entry targets.
    Object.entries(source.groups || {}).forEach(([key, profile]) => {
      if (!key || !profile) return;
      const presetId = makeReadingProfilePresetId(profile.label || readingProfileFriendlyKey(key), normalized.presets);
      normalized.presets[presetId] = normalizeReadingProfilePreset({ ...profile, id: presetId, name: profile.label || readingProfileFriendlyKey(key) }, presetId);
      normalized.groupAssignments[key] = presetId;
    });
    Object.entries(source.entries || {}).forEach(([key, profile]) => {
      if (!key || !profile) return;
      const presetId = makeReadingProfilePresetId(profile.label || readingProfileFriendlyKey(key), normalized.presets);
      normalized.presets[presetId] = normalizeReadingProfilePreset({ ...profile, id: presetId, name: profile.label || readingProfileFriendlyKey(key) }, presetId);
      normalized.entryAssignments[key] = presetId;
    });
  }

  if (!normalized.presets.default) normalized.presets.default = defaultReadingProfilePreset();
  normalized.presets.default = { ...defaultReadingProfilePreset(), ...(normalized.presets.default || {}), id: 'default', name: normalized.presets.default?.name || 'Default', builtIn: true };

  const importedDefault = String(source.defaultPresetId || normalized.defaultPresetId || 'default').trim();
  normalized.defaultPresetId = normalized.presets[importedDefault] ? importedDefault : 'default';

  Object.entries(source.groupAssignments || {}).forEach(([key, presetId]) => {
    if (key && normalized.presets[presetId]) normalized.groupAssignments[key] = presetId;
  });
  Object.entries(source.entryAssignments || {}).forEach(([key, presetId]) => {
    if (key && normalized.presets[presetId]) normalized.entryAssignments[key] = presetId;
  });

  Object.entries(normalized.groupAssignments || {}).forEach(([key, presetId]) => {
    if (!normalized.presets[presetId]) delete normalized.groupAssignments[key];
  });
  Object.entries(normalized.entryAssignments || {}).forEach(([key, presetId]) => {
    if (!normalized.presets[presetId]) delete normalized.entryAssignments[key];
  });

  return normalized;
}

function loadReadingProfiles() {
  try {
    const raw = localStorage.getItem(GUIDEVAULT_READING_PROFILES_KEY);
    const parsed = raw ? JSON.parse(raw) : {};
    const normalized = normalizeReadingProfiles(parsed);
    state.readingProfiles = normalized;
    return normalized;
  } catch {
    const empty = emptyReadingProfilesState();
    state.readingProfiles = empty;
    return empty;
  }
}

function saveReadingProfiles(profiles = state.readingProfiles) {
  const normalized = normalizeReadingProfiles(profiles || {});
  state.readingProfiles = normalized;
  localStorage.setItem(GUIDEVAULT_READING_PROFILES_KEY, JSON.stringify(normalized));
  return normalized;
}

function allReadingProfilePresets() {
  const profiles = state.readingProfiles || loadReadingProfiles();
  return Object.values(profiles.presets || {}).sort((a, b) => {
    if (a.id === 'default') return -1;
    if (b.id === 'default') return 1;
    return String(a.name || '').localeCompare(String(b.name || ''));
  });
}

function getReadingProfilePreset(presetId) {
  const profiles = state.readingProfiles || loadReadingProfiles();
  return profiles.presets?.[presetId] || profiles.presets?.[profiles.defaultPresetId] || profiles.presets?.default || defaultReadingProfilePreset();
}

function getDefaultReadingProfilePreset() {
  const profiles = state.readingProfiles || loadReadingProfiles();
  return getReadingProfilePreset(profiles.defaultPresetId || 'default');
}

function readingProfileGroupInfo(item) {
  const series = String(item?.series || '').trim();
  if (String(item?.kind || '') === 'Magazine' && series) {
    return { type: 'series', label: series, key: `series:${normalizeReadingProfileKeyPart(series)}` };
  }
  const category = categoryOf(item);
  return { type: 'category', label: category, key: `category:${normalizeReadingProfileKeyPart(category)}` };
}

function readingProfileEntryKey(item) {
  return String(item?.id || item?.Id || '').trim();
}

function getReadingProfileGroupTargets() {
  const map = new Map();
  (state.items || []).forEach(item => {
    const info = readingProfileGroupInfo(item);
    if (!info.key) return;
    const existing = map.get(info.key) || { ...info, count: 0 };
    existing.count += 1;
    existing.label = existing.label || info.label;
    existing.type = existing.type || info.type;
    map.set(info.key, existing);
  });
  const profiles = state.readingProfiles || loadReadingProfiles();
  Object.keys(profiles.groupAssignments || {}).forEach(key => {
    if (map.has(key)) return;
    const targetType = String(key).split(':')[0] || 'category';
    map.set(key, { key, type: targetType, label: readingProfileFriendlyKey(key), count: 0, storedOnly: true });
  });
  return [...map.values()].sort((a, b) => `${a.type}:${a.label}`.localeCompare(`${b.type}:${b.label}`));
}

function getReadingProfileEntryTargets() {
  return (state.items || [])
    .filter(item => readingProfileEntryKey(item))
    .map(item => ({ key: readingProfileEntryKey(item), label: displayTitle(item) || item.title || readingProfileEntryKey(item), item }))
    .sort((a, b) => a.label.localeCompare(b.label));
}

function readingProfileLabel(profile) {
  const normalized = normalizeReadingProfile(profile || defaultReadingProfile());
  const background = normalized.background ? readerBackgroundDisplayName(normalized.background) : 'Default Gradient';
  return `${displayModeLabel(normalized.displayMode)} • ${transitionLabel(normalized.transitionMode)} • ${background} • ${normalized.zoom}% zoom`;
}

function readingProfilePresetSummary(preset) {
  const normalized = normalizeReadingProfilePreset(preset || defaultReadingProfilePreset(), preset?.id || 'default');
  return `${normalized.name}: ${readingProfileLabel(normalized)}`;
}

function displayModeLabel(mode) {
  const normalized = normalizeReaderDisplayMode(mode);
  if (normalized === 1) return '1 Page';
  if (normalized === 3) return '2 Pages Adaptive';
  return '2 Pages';
}

function resolveReadingProfileForItem(item) {
  const profiles = state.readingProfiles || loadReadingProfiles();
  const group = readingProfileGroupInfo(item);
  const entryKey = readingProfileEntryKey(item);
  const groupProfileId = group?.key ? profiles.groupAssignments?.[group.key] : '';
  if (groupProfileId && profiles.presets?.[groupProfileId]) {
    return { profile: profiles.presets[groupProfileId], profileId: groupProfileId, source: group.type === 'series' ? 'Series profile' : 'Category profile', key: group.key, group };
  }
  const entryProfileId = entryKey ? profiles.entryAssignments?.[entryKey] : '';
  if (entryProfileId && profiles.presets?.[entryProfileId]) {
    return { profile: profiles.presets[entryProfileId], profileId: entryProfileId, source: 'Entry profile', key: entryKey, group };
  }
  const defaultProfileId = profiles.defaultPresetId || 'default';
  const defaultProfile = profiles.presets?.[defaultProfileId] || profiles.presets?.default || defaultReadingProfilePreset();
  return { profile: defaultProfile, profileId: defaultProfile.id || defaultProfileId, source: 'Default profile', key: '', group };
}

function profilePresetOptionsHtml(selectedId = '', options = {}) {
  const profiles = state.readingProfiles || loadReadingProfiles();
  const presets = allReadingProfilePresets();
  const chunks = [];
  if (options.includeClear) chunks.push(`<option value="">${escapeHtml(options.clearLabel || 'Inherit')}</option>`);
  presets.forEach(preset => {
    const defaultBadge = preset.id === profiles.defaultPresetId ? ' — Default' : '';
    const selected = String(preset.id) === String(selectedId || '') ? ' selected' : '';
    chunks.push(`<option value="${escapeHtml(preset.id)}"${selected}>${escapeHtml(preset.name || 'Reading Profile')}${escapeHtml(defaultBadge)}</option>`);
  });
  return chunks.join('');
}

function syncReadingProfileBackgroundOptions() {
  const options = ['<option value="">Default Gradient</option>'].concat((state.reader.backgrounds || []).map(bg => `<option value="${escapeHtml(bg.name)}">${escapeHtml(bg.displayName || readerBackgroundDisplayName(bg.name))}</option>`));
  ['readingProfilePresetBackground'].forEach(id => {
    const select = $(id);
    if (!select) return;
    const current = select.value;
    select.innerHTML = options.join('');
    if ([...select.options].some(opt => opt.value === current)) select.value = current;
  });
}

function setReadingProfileStatus(message = '', tone = '') {
  const el = $('readingProfileStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}

function setDetailReadingProfileStatus(message = '', tone = '') {
  const el = $('detailReadingProfileStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}

function readingProfilePresetFormIds() {
  return {
    name: 'readingProfilePresetName',
    display: 'readingProfilePresetDisplay',
    transition: 'readingProfilePresetTransition',
    background: 'readingProfilePresetBackground',
    zoom: 'readingProfilePresetZoom',
    zoomValue: 'readingProfilePresetZoomValue',
    selector: 'readingProfilePresetSelect'
  };
}

function setReadingProfilePresetFormValues(preset = null) {
  const ids = readingProfilePresetFormIds();
  const normalized = normalizeReadingProfilePreset(preset || defaultReadingProfilePreset(), preset?.id || 'default');
  if ($(ids.name)) $(ids.name).value = normalized.name || '';
  if ($(ids.display)) $(ids.display).value = String(normalized.displayMode);
  if ($(ids.transition)) $(ids.transition).value = normalized.transitionMode;
  if ($(ids.background)) $(ids.background).value = normalized.background;
  if ($(ids.zoom)) $(ids.zoom).value = String(normalized.zoom);
  if ($(ids.zoomValue)) $(ids.zoomValue).textContent = `${normalized.zoom}%`;
  if ($('readingProfileDeletePreset')) $('readingProfileDeletePreset').disabled = normalized.id === 'default';
}

function getReadingProfilePresetFormValues() {
  const ids = readingProfilePresetFormIds();
  return normalizeReadingProfile({
    displayMode: $(ids.display)?.value,
    transitionMode: $(ids.transition)?.value,
    background: $(ids.background)?.value,
    zoom: $(ids.zoom)?.value
  });
}

function refreshReadingProfilePresetZoomOutput() {
  const ids = readingProfilePresetFormIds();
  const zoom = clampNumber($(ids.zoom)?.value, 70, 145, 100);
  if ($(ids.zoomValue)) $(ids.zoomValue).textContent = `${zoom}%`;
}

function fillReadingProfilePresetSelector(preferredId = '') {
  const select = $('readingProfilePresetSelect');
  if (!select) return;
  const current = preferredId || select.value || '';
  select.innerHTML = profilePresetOptionsHtml(current);
  const profiles = state.readingProfiles || loadReadingProfiles();
  select.value = [...select.options].some(opt => opt.value === current) ? current : (profiles.defaultPresetId || 'default');
}

function renderReadingProfilePresetList() {
  const list = $('readingProfilePresetList');
  if (!list) return;
  const profiles = state.readingProfiles || loadReadingProfiles();
  const presets = allReadingProfilePresets();
  if (!presets.length) {
    list.innerHTML = '<p class="sub">No reading profile presets yet.</p>';
    return;
  }
  list.innerHTML = presets.map(preset => {
    const badge = preset.id === profiles.defaultPresetId ? '<span class="pill">Default</span>' : '';
    return `<button class="reading-profile-preset-row" type="button" data-profile-id="${escapeHtml(preset.id)}"><span><b>${escapeHtml(preset.name || 'Reading Profile')}</b><em>${escapeHtml(readingProfileLabel(preset))}</em></span>${badge}</button>`;
  }).join('');
}

function loadSelectedReadingProfilePresetForm() {
  const profiles = state.readingProfiles || loadReadingProfiles();
  const id = $('readingProfilePresetSelect')?.value || profiles.defaultPresetId || 'default';
  setReadingProfilePresetFormValues(profiles.presets?.[id] || getDefaultReadingProfilePreset());
}

function renderReadingProfileSettings(preferredId = '') {
  loadReadingProfiles();
  syncReadingProfileBackgroundOptions();
  fillReadingProfilePresetSelector(preferredId);
  loadSelectedReadingProfilePresetForm();
  renderReadingProfilePresetList();
}

function createReadingProfilePreset() {
  const profiles = loadReadingProfiles();
  const id = makeReadingProfilePresetId('Custom Profile', profiles.presets);
  profiles.presets[id] = normalizeReadingProfilePreset({ id, name: 'Custom Profile', ...currentReaderSettingsAsProfile(), updatedAt: new Date().toISOString() }, id);
  saveReadingProfiles(profiles);
  renderReadingProfileSettings(id);
  setReadingProfileStatus('New reading profile preset created. Rename it and save when ready.', 'success');
}

function saveReadingProfilePreset() {
  const profiles = loadReadingProfiles();
  const ids = readingProfilePresetFormIds();
  const selectedId = $(ids.selector)?.value || 'default';
  const existing = profiles.presets?.[selectedId] || defaultReadingProfilePreset();
  const name = String($(ids.name)?.value || '').trim();
  if (!name) { setReadingProfileStatus('Give this reading profile a name before saving.', 'error'); return; }
  profiles.presets[selectedId] = {
    ...existing,
    ...getReadingProfilePresetFormValues(),
    id: selectedId,
    name,
    builtIn: selectedId === 'default' || !!existing.builtIn,
    updatedAt: new Date().toISOString()
  };
  saveReadingProfiles(profiles);
  renderReadingProfileSettings(selectedId);
  if (state.selected) renderDetailReadingProfilePanel(state.selected);
  setReadingProfileStatus('Reading profile preset saved.', 'success');
}

function deleteReadingProfilePreset() {
  const profiles = loadReadingProfiles();
  const id = $('readingProfilePresetSelect')?.value || '';
  if (!id || id === 'default') { setReadingProfileStatus('The Default reading profile cannot be deleted.', 'error'); return; }
  if (!profiles.presets?.[id]) return;
  delete profiles.presets[id];
  Object.entries(profiles.groupAssignments || {}).forEach(([key, presetId]) => { if (presetId === id) delete profiles.groupAssignments[key]; });
  Object.entries(profiles.entryAssignments || {}).forEach(([key, presetId]) => { if (presetId === id) delete profiles.entryAssignments[key]; });
  if (profiles.defaultPresetId === id) profiles.defaultPresetId = 'default';
  saveReadingProfiles(profiles);
  renderReadingProfileSettings(profiles.defaultPresetId || 'default');
  if (state.selected) renderDetailReadingProfilePanel(state.selected);
  setReadingProfileStatus('Reading profile preset deleted and removed from assignments.', 'info');
}

function setDefaultReadingProfilePreset() {
  const profiles = loadReadingProfiles();
  const id = $('readingProfilePresetSelect')?.value || '';
  if (!id || !profiles.presets?.[id]) { setReadingProfileStatus('Choose a profile preset before setting it as default.', 'error'); return; }
  profiles.defaultPresetId = id;
  saveReadingProfiles(profiles);
  renderReadingProfileSettings(id);
  if (state.selected) renderDetailReadingProfilePanel(state.selected);
  setReadingProfileStatus(`${profiles.presets[id].name || 'Reading profile'} is now the default profile.`, 'success');
}

function renderDetailReadingProfilePanel(item) {
  if (!item) return;
  const profiles = loadReadingProfiles();
  const group = readingProfileGroupInfo(item);
  const entryKey = readingProfileEntryKey(item);
  const groupProfileId = group?.key ? (profiles.groupAssignments?.[group.key] || '') : '';
  const entryProfileId = entryKey ? (profiles.entryAssignments?.[entryKey] || '') : '';

  if ($('detailReadingProfileGroupLabel')) {
    const typeLabel = group?.type === 'series' ? 'Series' : 'Category';
    $('detailReadingProfileGroupLabel').textContent = `${typeLabel}: ${group?.label || 'Unsorted'}`;
  }
  if ($('detailGroupProfileSelect')) $('detailGroupProfileSelect').innerHTML = profilePresetOptionsHtml(groupProfileId, { includeClear: true, clearLabel: 'Use global default profile' });
  if ($('detailGroupProfileSelect')) $('detailGroupProfileSelect').value = groupProfileId;
  if ($('detailEntryProfileSelect')) $('detailEntryProfileSelect').innerHTML = profilePresetOptionsHtml(entryProfileId, { includeClear: true, clearLabel: 'Inherit from series/category/default' });
  if ($('detailEntryProfileSelect')) $('detailEntryProfileSelect').value = entryProfileId;
  updateDetailReadingProfileEffectivePreview(item);
}

function updateDetailReadingProfileEffectivePreview(item = state.selected) {
  const summary = $('detailReadingProfileEffectiveSummary');
  if (!summary || !item) return;
  const profiles = state.readingProfiles || loadReadingProfiles();
  const group = readingProfileGroupInfo(item);
  const entryKey = readingProfileEntryKey(item);
  const storedGroupProfileId = group?.key ? profiles.groupAssignments?.[group.key] : '';
  const storedEntryProfileId = entryKey ? profiles.entryAssignments?.[entryKey] : '';
  const groupProfileId = $('detailGroupProfileSelect') ? $('detailGroupProfileSelect').value : storedGroupProfileId;
  const entryProfileId = $('detailEntryProfileSelect') ? $('detailEntryProfileSelect').value : storedEntryProfileId;

  let resolved;
  if (groupProfileId && profiles.presets?.[groupProfileId]) {
    resolved = { profile: profiles.presets[groupProfileId], profileId: groupProfileId, source: group?.type === 'series' ? 'Series profile' : 'Category profile', key: group?.key || '', group };
  } else if (entryProfileId && profiles.presets?.[entryProfileId]) {
    resolved = { profile: profiles.presets[entryProfileId], profileId: entryProfileId, source: 'Entry profile', key: entryKey, group };
  } else {
    const defaultProfileId = profiles.defaultPresetId || 'default';
    const defaultProfile = profiles.presets?.[defaultProfileId] || profiles.presets?.default || defaultReadingProfilePreset();
    resolved = { profile: defaultProfile, profileId: defaultProfile.id || defaultProfileId, source: 'Default profile', key: '', group };
  }

  const maskedText = resolved.source !== 'Entry profile' && entryProfileId
    ? ' An entry profile is assigned, but it is currently masked because the series/category layer takes precedence.'
    : '';
  const unsavedText = (groupProfileId !== storedGroupProfileId || entryProfileId !== storedEntryProfileId)
    ? ' Preview includes unsaved assignment changes.'
    : '';
  const inheritedFrom = resolved.source === 'Default profile'
    ? `Inherited from the global default preset.`
    : resolved.source === 'Entry profile'
      ? `Applied directly to this entry.`
      : `Inherited from ${group?.type === 'series' ? 'series' : 'category'} “${group?.label || 'Unsorted'}”.`;
  summary.textContent = `${resolved.source}: ${readingProfilePresetSummary(resolved.profile)}. ${inheritedFrom}${maskedText}${unsavedText}`;
}

function saveDetailReadingProfileAssignment(scope) {
  if (!state.selected) return;
  const profiles = loadReadingProfiles();
  const item = state.selected;
  const group = readingProfileGroupInfo(item);
  const entryKey = readingProfileEntryKey(item);
  const select = scope === 'group' ? $('detailGroupProfileSelect') : $('detailEntryProfileSelect');
  const profileId = select?.value || '';
  if (profileId && !profiles.presets?.[profileId]) { setDetailReadingProfileStatus('Choose an existing reading profile preset first.', 'error'); return; }

  if (scope === 'group') {
    if (!group?.key) return;
    if (profileId) profiles.groupAssignments[group.key] = profileId;
    else delete profiles.groupAssignments[group.key];
  } else {
    if (!entryKey) return;
    if (profileId) profiles.entryAssignments[entryKey] = profileId;
    else delete profiles.entryAssignments[entryKey];
  }

  saveReadingProfiles(profiles);
  renderDetailReadingProfilePanel(item);
  const savedPreset = profileId ? (profiles.presets[profileId]?.name || 'selected preset') : 'inheritance/default';
  setDetailReadingProfileStatus(`${scope === 'group' ? 'Series/category' : 'Entry'} reading profile assignment saved: ${savedPreset}.`, 'success');
}

function clearDetailReadingProfileAssignment(scope) {
  if (!state.selected) return;
  const select = scope === 'group' ? $('detailGroupProfileSelect') : $('detailEntryProfileSelect');
  if (select) select.value = '';
  saveDetailReadingProfileAssignment(scope);
}

function updateReadingProfileEffectivePreview() {
  // Kept for compatibility with the first reading-profile scaffold; the refined
  // layout now renders the effective preview in the item details Reading Profile tab.
  if (state.selected) updateDetailReadingProfileEffectivePreview(state.selected);
}

function applyReadingProfileToReader(item) {
  loadReadingProfiles();
  const resolved = resolveReadingProfileForItem(item);
  if (!resolved.profile) return;
  const profile = normalizeReadingProfile(resolved.profile);
  state.reader.displayMode = profile.displayMode;
  state.reader.transitionMode = profile.transitionMode;
  state.reader.background = profile.background;
  state.reader.zoom = profile.zoom;
}


function favoriteItemKey(itemOrId) {
  if (itemOrId && typeof itemOrId === 'object') return String(itemOrId.id || itemOrId.Id || '').trim();
  return String(itemOrId || '').trim();
}
function loadFavorites() {
  try {
    const raw = JSON.parse(localStorage.getItem(GUIDEVAULT_FAVORITES_KEY) || '{}') || {};
    const map = {};
    Object.keys(raw).forEach(key => { if (raw[key]) map[String(key)] = true; });
    state.favorites = map;
  } catch {
    state.favorites = {};
  }
  return state.favorites;
}
function saveFavorites() {
  try { localStorage.setItem(GUIDEVAULT_FAVORITES_KEY, JSON.stringify(state.favorites || {})); } catch {}
}
function isFavoriteItem(itemOrId) {
  const key = favoriteItemKey(itemOrId);
  if (!key) return false;
  if (!state.favorites) loadFavorites();
  return !!state.favorites?.[key];
}
function setFavoriteItem(itemOrId, favorite) {
  const key = favoriteItemKey(itemOrId);
  if (!key) return false;
  if (!state.favorites) loadFavorites();
  if (favorite) state.favorites[key] = true;
  else delete state.favorites[key];
  saveFavorites();
  return !!state.favorites[key];
}
function toggleFavoriteItem(itemOrId) {
  return setFavoriteItem(itemOrId, !isFavoriteItem(itemOrId));
}
function updateFavoriteVisuals(itemId) {
  const isFav = isFavoriteItem(itemId);
  document.querySelectorAll(`.favorite[data-id="${CSS.escape(String(itemId || ''))}"]`).forEach(btn => {
    btn.classList.toggle('active', isFav);
    btn.setAttribute('aria-pressed', isFav ? 'true' : 'false');
    btn.title = isFav ? 'Remove from favorites' : 'Add to favorites';
  });
  if ($('countFavs')) $('countFavs').textContent = Object.keys(state.favorites || {}).length;
}
function handleFavoriteClick(event) {
  const btn = event.target.closest?.('.favorite[data-id]');
  if (!btn) return;
  event.preventDefault();
  event.stopPropagation();
  event.stopImmediatePropagation?.();
  const itemId = btn.dataset.id || '';
  toggleFavoriteItem(itemId);
  updateFavoriteVisuals(itemId);
  if (state.filter === 'Favorites' || state.viewMode === 'favorites') applyFilters();
}

function normalizeLoginProfile(value = {}) {
  return {
    username: String(value.username || '').trim(),
    email: String(value.email || '').trim(),
    password: String(value.password || ''),
    avatarDataUrl: String(value.avatarDataUrl || ''),
    createdAt: value.createdAt || new Date().toISOString(),
    updatedAt: value.updatedAt || new Date().toISOString()
  };
}
function readLoginProfile() {
  try {
    const raw = localStorage.getItem(GUIDEVAULT_LOGIN_PROFILE_KEY);
    if (!raw) return null;
    const profile = normalizeLoginProfile(JSON.parse(raw));
    return profile.username && profile.email && profile.password ? profile : null;
  } catch {
    return null;
  }
}
function saveLoginProfile(profile) {
  const existing = readLoginProfile();
  const normalized = normalizeLoginProfile({
    ...(existing || {}),
    ...profile,
    avatarDataUrl: profile.avatarDataUrl !== undefined ? profile.avatarDataUrl : (existing?.avatarDataUrl || ''),
    createdAt: existing?.createdAt || profile.createdAt || new Date().toISOString(),
    updatedAt: new Date().toISOString()
  });
  localStorage.setItem(GUIDEVAULT_LOGIN_PROFILE_KEY, JSON.stringify(normalized));
  state.auth.profile = normalized;
  return normalized;
}
function getProfileFormValues(prefix) {
  return {
    username: $(`${prefix}Username`)?.value?.trim() || '',
    email: $(`${prefix}Email`)?.value?.trim() || '',
    password: $(`${prefix}Password`)?.value || '',
    avatarDataUrl: state.auth.profile?.avatarDataUrl || readLoginProfile()?.avatarDataUrl || ''
  };
}
function validateLoginProfile(profile) {
  if (!profile.username || !profile.email || !profile.password) return 'Username, email, and password are required.';
  if (!profile.email.includes('@') || profile.email.startsWith('@') || profile.email.endsWith('@')) return 'Enter a valid email address.';
  return '';
}
function setLoginStatus(message = '', tone = '') {
  const el = $('loginStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}
function setAccountStatus(message = '', tone = '') {
  const el = $('accountStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}
function updateLoginPageMode() {
  const profile = readLoginProfile();
  const firstUse = !profile;
  const title = $('loginTitle');
  const subtitle = $('loginSubtitle');
  const help = $('loginHelp');
  if (title) {
    title.textContent = 'Create Guidevault Login';
    title.classList.toggle('hidden', !firstUse);
  }
  if (subtitle) {
    subtitle.textContent = 'Create the local login profile for this Guidevault browser.';
    subtitle.classList.toggle('hidden', !firstUse);
  }
  if (help) {
    help.textContent = '';
    help.classList.add('hidden');
  }
  if ($('loginAction')) $('loginAction').textContent = firstUse ? 'Create Local Login' : 'Sign In';
  if (!firstUse) {
    if ($('loginUsername')) $('loginUsername').value = profile.username || '';
    if ($('loginEmail')) $('loginEmail').value = profile.email || '';
  }
  if ($('loginPassword')) $('loginPassword').value = '';
  setLoginStatus('');
}
function showLoginScreen(message = '') {
  state.auth.authenticated = false;
  document.body.classList.add('auth-locked');
  if ($('app')) $('app').classList.add('hidden');
  if ($('loginView')) $('loginView').classList.remove('hidden');
  updateLoginPageMode();
  if (message) setLoginStatus(message, 'info');
  requestAnimationFrame(() => ($('loginPassword') || $('loginUsername'))?.focus?.());
}

function userInitials(profile = state.auth.profile || readLoginProfile() || {}) {
  const value = String(profile.username || profile.email || 'GV').trim();
  if (!value) return 'GV';
  const parts = value.replace(/@.*$/, '').split(/[\s._-]+/).filter(Boolean);
  if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
  return value.slice(0, 2).toUpperCase();
}
function renderUserAvatarElement(el, profile = state.auth.profile || readLoginProfile() || {}) {
  if (!el) return;
  const avatar = String(profile.avatarDataUrl || '').trim();
  el.textContent = avatar ? '' : userInitials(profile);
  el.classList.toggle('has-image', !!avatar);
  if (avatar) el.style.backgroundImage = `url("${avatar.replace(/"/g, '%22')}")`;
  else el.style.removeProperty('background-image');
}
function syncTopUserMenu() {
  const profile = state.auth.profile || readLoginProfile() || {};
  const label = profile.username || profile.email || 'User';
  if ($('topCurrentUser')) $('topCurrentUser').textContent = label;
  renderUserAvatarElement($('topUserAvatar'), profile);
  const btn = $('userMenuBtn');
  if (btn) btn.title = label ? `Guidevault user: ${label}` : 'Guidevault user menu';
}
function setUserMenuOpen(open) {
  const panel = $('userMenuPanel');
  const btn = $('userMenuBtn');
  if (!panel || !btn) return;
  panel.classList.toggle('hidden', !open);
  btn.setAttribute('aria-expanded', open ? 'true' : 'false');
}
function toggleUserMenu() {
  syncTopUserMenu();
  setUserMenuOpen($('userMenuPanel')?.classList.contains('hidden'));
}

function renderAccountProfile() {
  const profile = state.auth.profile || readLoginProfile();
  if (!profile) return;
  if ($('accountUsername')) $('accountUsername').value = profile.username || '';
  if ($('accountEmail')) $('accountEmail').value = profile.email || '';
  if ($('accountPassword')) $('accountPassword').value = profile.password || '';
  if ($('accountProfileSummary')) $('accountProfileSummary').textContent = `${profile.username} • ${profile.email}`;
  renderUserAvatarElement($('accountAvatarPreview'), profile);
  syncTopUserMenu();
  setAccountEditMode(false, false);
}

function formatProfileRelativeTime(value) {
  const time = dateValue(value);
  if (!time) return '—';
  const diff = Date.now() - time;
  if (diff < 86400000) return 'today';
  const days = Math.max(1, Math.floor(diff / 86400000));
  if (days < 31) return `${days} day${days === 1 ? '' : 's'} ago`;
  const months = Math.floor(days / 30);
  if (months < 24) return `${months} month${months === 1 ? '' : 's'} ago`;
  const years = Math.floor(days / 365);
  return `${years} year${years === 1 ? '' : 's'} ago`;
}
function profileEventsForRange() {
  const range = state.profilePage?.range || 'all';
  const profile = state.auth.profile || readLoginProfile() || {};
  const userKey = String(profile.username || profile.email || 'local user').toLowerCase();
  const cutoff = range === 'month' ? Date.now() - 31 * 86400000 : range === 'year' ? new Date(new Date().getFullYear(), 0, 1).getTime() : 0;
  return readReadingActivity().filter(e => {
    const eventUser = String(e.user || userKey).toLowerCase();
    return eventUser === userKey && (!cutoff || dateValue(e.at) >= cutoff);
  });
}
function profileItemLookup() {
  return new Map((state.items || []).map(item => [String(item.id || item.Id || ''), item]));
}
function profilePageStats() {
  const profile = state.auth.profile || readLoginProfile() || {};
  const events = profileEventsForRange();
  const lookup = profileItemLookup();
  const uniqueIds = [...new Set(events.map(e => String(e.id || '')).filter(Boolean))];
  const uniqueItems = uniqueIds.map(id => lookup.get(id)).filter(Boolean);
  const fallbackItems = uniqueItems.length ? uniqueItems : events.map(e => ({ kind: e.kind, title: e.title, pageCount: 0 }));
  const pages = fallbackItems.reduce((sum, item) => sum + (Number(item.pageCount || item.pages || item.PageCount || 0) || 0), 0);
  const estimatedMinutes = Math.max(events.length * 5, Math.round(pages * 1.6));
  const joined = dateValue(profile.createdAt) || Date.now();
  const weeks = Math.max(1, Math.ceil((Date.now() - joined) / (7 * 86400000)));
  return {
    profile,
    events,
    lookup,
    uniqueIds,
    uniqueItems,
    totalReads: events.length,
    manuals: events.filter(e => normalizeReadingKindGroup(e.kind) === 'Manual').length,
    strategyGuides: events.filter(e => normalizeReadingKindGroup(e.kind) === 'Strategy Guide').length,
    magazines: events.filter(e => normalizeReadingKindGroup(e.kind) === 'Magazine').length,
    pages,
    words: Math.round(pages * 420),
    authors: new Set(uniqueItems.map(i => i.writer || i.author || i.publisher).filter(Boolean)).size,
    ratings: Object.keys(state.favorites || {}).length,
    estimatedMinutes,
    avgPerWeek: estimatedMinutes / weeks,
    joined,
    lastRead: events.reduce((max, e) => Math.max(max, dateValue(e.at) || 0), 0)
  };
}
function formatProfileMinutes(minutes) {
  const n = Math.max(0, Math.round(Number(minutes || 0)));
  if (n >= 60) return `${(n / 60).toFixed(1)} hours`;
  return `${n} minutes`;
}
function renderProfileMetricStrip(stats) {
  const host = $('profileMetricStrip');
  if (!host) return;
  const metrics = [
    ['Manuals Read', stats.manuals, '▤'],
    ['Strategy Guides Read', stats.strategyGuides, '⌖'],
    ['Magazines Read', stats.magazines, '▥'],
    ['Pages Read', stats.pages.toLocaleString(), '◻'],
    ['Words Read', stats.words.toLocaleString(), '▦'],
    ['Authors / Publishers', stats.authors, '♚'],
    ['Favorites', stats.ratings, '★']
  ];
  host.innerHTML = metrics.map(([label, value, icon]) => `<div class="profile-metric"><span>${escapeHtml(label)}</span><i>${escapeHtml(icon)}</i><strong>${escapeHtml(String(value))}</strong></div>`).join('');
}
function heatmapStartDate(year) {
  const start = new Date(year, 0, 1);
  start.setDate(start.getDate() - start.getDay());
  return start;
}
function renderProfileHeatmap(stats) {
  const host = $('profileHeatmap');
  if (!host) return;
  const year = new Date().getFullYear();
  const byDay = new Map();
  stats.events.forEach(e => {
    const d = new Date(e.at || '');
    if (Number.isNaN(d.getTime()) || d.getFullYear() !== year) return;
    const key = `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
    byDay.set(key, (byDay.get(key) || 0) + 1);
  });
  const start = heatmapStartDate(year);
  const today = new Date();
  let cells = '';
  for (let w = 0; w < 53; w++) {
    cells += '<div class="profile-heatmap-week">';
    for (let d = 0; d < 7; d++) {
      const current = new Date(start);
      current.setDate(start.getDate() + w * 7 + d);
      const inYear = current.getFullYear() === year;
      const future = current > today;
      const key = `${current.getFullYear()}-${String(current.getMonth()+1).padStart(2,'0')}-${String(current.getDate()).padStart(2,'0')}`;
      const count = byDay.get(key) || 0;
      const level = !inYear || future ? 0 : Math.min(4, count);
      const label = `${current.toLocaleDateString()}: ${count} read event${count === 1 ? '' : 's'}`;
      cells += `<span class="profile-heatmap-cell" data-level="${level}" title="${escapeForAttribute(label)}"></span>`;
    }
    cells += '</div>';
  }
  host.innerHTML = `<div class="profile-heatmap-months"><span>Jan</span><span>Feb</span><span>Mar</span><span>Apr</span><span>May</span><span>Jun</span><span>Jul</span><span>Aug</span><span>Sep</span><span>Oct</span><span>Nov</span><span>Dec</span></div><div class="profile-heatmap-grid"><div class="profile-heatmap-days"><span>Sun</span><span>Tue</span><span>Thu</span><span>Sat</span></div><div class="profile-heatmap-weeks">${cells}</div></div>`;
  const pagesThisYear = stats.uniqueItems.reduce((sum, item) => sum + (Number(item.pageCount || item.pages || item.PageCount || 0) || 0), 0);
  setText('profileHeatmapKicker', `READING ACTIVITY ${year}`);
  setText('profileHeatmapSummary', stats.events.length ? `You read ${pagesThisYear.toLocaleString()} pages and ${Math.round(pagesThisYear * 420).toLocaleString()} words in ${year}` : `No reading activity logged in ${year} yet.`);
}
function renderProfileRecent(stats) {
  const host = $('profileRecentReads');
  if (!host) return;
  const recent = [...new Set(stats.events.slice().reverse().map(e => String(e.id || '')).filter(Boolean))]
    .map(id => stats.lookup.get(id)).filter(Boolean).slice(0, 6);
  host.innerHTML = recent.length ? recent.map(item => `<article class="profile-recent-card"><img loading="lazy" src="${coverUrl(item)}" alt="" /><div><strong>${escapeHtml(displayTitle(item))}</strong><span>${escapeHtml(item.kind || '')} • ${escapeHtml(preferredPlatformOf(item) || categoryOf(item) || '—')}</span></div></article>`).join('') : '<article class="settings-card"><p class="sub">Open a manual, guide, or magazine to start filling out recent reads.</p></article>';
}
function renderProfileTopLists(stats) {
  const host = $('profileTopLists');
  if (!host) return;
  const events = stats.events;
  host.innerHTML = [
    ['Top Reads', countBy(events, e => e.title || 'Unknown'), 'reads'],
    ['By Content Type', countBy(events, e => normalizeReadingKindGroup(e.kind) || 'Unknown'), 'events'],
    ['Recent Platforms', countBy(stats.uniqueItems, item => libraryCategoryKeysForItem(item)), 'items']
  ].map(([title, rows, unit]) => `<article class="settings-card profile-top-card"><h2>${escapeHtml(title)}</h2>${topItemsHtml(rows, unit, 8)}</article>`).join('');
}
function renderProfileActivityList(stats) {
  const host = $('profileActivityList');
  if (!host) return;
  const rows = stats.events.slice().reverse().slice(0, 50);
  host.innerHTML = rows.length ? rows.map(e => `<article class="settings-card profile-activity-row"><strong>${escapeHtml(e.title || 'Unknown item')}</strong><span>${escapeHtml(e.kind || 'Item')} • ${escapeHtml(e.action || 'view')}</span><em>${escapeHtml(e.at ? new Date(e.at).toLocaleString() : 'Unknown time')}</em></article>`).join('') : '<article class="settings-card"><p class="sub">No activity is logged for this range yet.</p></article>';
}
function setProfileTab(tab = 'overview') {
  state.profilePage.activeTab = ['overview','stats','reviews','activity'].includes(tab) ? tab : 'overview';
  document.querySelectorAll('.profile-tab').forEach(btn => btn.classList.toggle('active', (btn.dataset.profileTab || '') === state.profilePage.activeTab));
  $('profileOverviewPanel')?.classList.toggle('hidden', state.profilePage.activeTab !== 'overview');
  $('profileStatsPanel')?.classList.toggle('hidden', state.profilePage.activeTab !== 'stats');
  $('profileReviewsPanel')?.classList.toggle('hidden', state.profilePage.activeTab !== 'reviews');
  $('profileActivityPanel')?.classList.toggle('hidden', state.profilePage.activeTab !== 'activity');
  renderPersonalProfile();
}
function renderPersonalProfile() {
  if (!$('profileView')) return;
  if (!state.favorites) loadFavorites();
  const stats = profilePageStats();
  const profile = stats.profile;
  renderUserAvatarElement($('profilePageAvatar'), profile);
  setText('profilePageName', profile.username || profile.email || 'Guidevault User');
  setText('profileReadBadge', `${stats.totalReads} Read${stats.totalReads === 1 ? '' : 's'}`);
  setText('profileJoined', formatProfileRelativeTime(stats.joined));
  setText('profileLastRead', stats.lastRead ? formatProfileRelativeTime(stats.lastRead) : 'No reads yet');
  setText('profileTotalReadTime', formatProfileMinutes(stats.estimatedMinutes));
  setText('profileAvgPerWeek', `${Math.max(0, stats.avgPerWeek).toFixed(1)} minutes`);
  setText('profileOverviewTitle', `A look at ${(profile.username || 'your')}’s journey through Guidevault`);
  if ($('profileRange')) $('profileRange').value = state.profilePage.range || 'all';
  renderProfileMetricStrip(stats);
  renderProfileHeatmap(stats);
  renderProfileRecent(stats);
  renderProfileTopLists(stats);
  renderProfileActivityList(stats);
}
function showUserProfilePage(options = {}) {
  cleanupInactiveViewsForNavigation('profile');
  clearColorscapeDetailTheme();
  document.body.classList.remove(
    'detail-page-mode',
    'reader-page-mode',
    'settings-sidebar-mode',
    'strategy-detail-mode',
    'magazine-detail-mode',
    'manual-detail-mode'
  );
  document.body.classList.add('profile-page-mode');
  hideAppView('readerView');
  hideAppView('detailView');
  hideAppView('libraryView');
  hideAppView('settingsView');
  showAppView('profileView', 'block');
  if ($('profileView')) $('profileView').scrollTop = 0;
  state.profilePage = state.profilePage || { activeTab: 'overview', range: 'all' };
  state.profilePage.activeTab = state.profilePage.activeTab || 'overview';
  renderPersonalProfile();
  setProfileTab(state.profilePage.activeTab);
  if (!options.skipHash && window.location.hash !== '#profile') {
    try { history.replaceState(null, '', '#profile'); } catch {}
  }
}
function showAuthenticatedApp() {
  state.auth.profile = readLoginProfile();
  state.auth.authenticated = true;
  document.body.classList.remove('auth-locked');
  if ($('loginView')) $('loginView').classList.add('hidden');
  if ($('app')) $('app').classList.remove('hidden');
  renderAccountProfile();
  syncTopUserMenu();
  startDeviceHeartbeat();
  if (!state.auth.appStarted) {
    state.auth.appStarted = true;
    loadLibrary();
    startStableUpdatePolling();
    if (window.location.hash === '#profile') {
      requestAnimationFrame(() => showUserProfilePage({ skipHash: true }));
    }
  }
}
function setAccountEditMode(editing, clearStatus = true) {
  state.auth.editing = !!editing;
  ['accountUsername', 'accountEmail', 'accountPassword'].forEach(id => {
    const input = $(id);
    if (input) input.disabled = !editing;
  });
  if ($('accountEditLogin')) $('accountEditLogin').classList.toggle('hidden', editing);
  if ($('accountSaveLogin')) $('accountSaveLogin').classList.toggle('hidden', !editing);
  if ($('accountCancelEdit')) $('accountCancelEdit').classList.toggle('hidden', !editing);
  if (clearStatus) setAccountStatus(editing ? 'Editing local login profile.' : '');
}
function handleLoginSubmit(e) {
  e?.preventDefault?.();
  const existing = readLoginProfile();
  const form = getProfileFormValues('login');
  const validation = validateLoginProfile(form);
  if (validation) { setLoginStatus(validation, 'error'); return; }
  if (!existing) {
    saveLoginProfile(form);
    setLoginStatus('Local login created.', 'success');
    showAuthenticatedApp();
    return;
  }
  const matches = form.username === existing.username && form.email === existing.email && form.password === existing.password;
  if (!matches) {
    setLoginStatus('The username, email, or password does not match the saved local profile.', 'error');
    return;
  }
  state.auth.profile = existing;
  showAuthenticatedApp();
}
function saveAccountLoginFromSettings() {
  const form = getProfileFormValues('account');
  const validation = validateLoginProfile(form);
  if (validation) { setAccountStatus(validation, 'error'); return; }
  saveLoginProfile(form);
  renderAccountProfile();
  syncTopUserMenu();
  setAccountStatus('Login profile saved.', 'success');
}

function resizeProfilePicFile(file) {
  return new Promise((resolve, reject) => {
    if (!file || !/^image\//i.test(file.type || '')) { reject(new Error('Choose an image file.')); return; }
    const reader = new FileReader();
    reader.onerror = () => reject(new Error('Could not read the profile picture.'));
    reader.onload = () => {
      const img = new Image();
      img.onload = () => {
        const size = 256;
        const canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        const ctx = canvas.getContext('2d');
        if (!ctx) { reject(new Error('Could not prepare the profile picture.')); return; }
        const scale = Math.max(size / img.width, size / img.height);
        const width = img.width * scale;
        const height = img.height * scale;
        ctx.drawImage(img, (size - width) / 2, (size - height) / 2, width, height);
        resolve(canvas.toDataURL('image/jpeg', 0.86));
      };
      img.onerror = () => reject(new Error('Could not load the profile picture.'));
      img.src = String(reader.result || '');
    };
    reader.readAsDataURL(file);
  });
}

async function handleAccountProfilePicFile(file) {
  try {
    const avatarDataUrl = await resizeProfilePicFile(file);
    const profile = saveLoginProfile({ ...(state.auth.profile || readLoginProfile() || {}), avatarDataUrl });
    renderAccountProfile();
    renderUserAvatarElement($('accountAvatarPreview'), profile);
    syncTopUserMenu();
    setAccountStatus('Profile picture updated.', 'success');
  } catch (err) {
    setAccountStatus(err?.message || 'Profile picture could not be updated.', 'error');
  } finally {
    if ($('accountProfilePicInput')) $('accountProfilePicInput').value = '';
  }
}

function removeAccountProfilePic() {
  const profile = saveLoginProfile({ ...(state.auth.profile || readLoginProfile() || {}), avatarDataUrl: '' });
  renderAccountProfile();
  renderUserAvatarElement($('accountAvatarPreview'), profile);
  syncTopUserMenu();
  setAccountStatus('Profile picture removed.', 'info');
}


function defaultGuidevaultPreferences() {
  return { useColorscape: false };
}

function normalizeGuidevaultPreferences(value = {}) {
  return {
    useColorscape: value.useColorscape === true || String(value.useColorscape || '').toLowerCase() === 'true'
  };
}

function loadGuidevaultPreferences() {
  try {
    const raw = localStorage.getItem(GUIDEVAULT_PREFERENCES_KEY);
    state.preferences = normalizeGuidevaultPreferences(raw ? JSON.parse(raw) : defaultGuidevaultPreferences());
  } catch {
    state.preferences = defaultGuidevaultPreferences();
  }
  return state.preferences;
}

function saveGuidevaultPreferences(preferences = state.preferences) {
  state.preferences = normalizeGuidevaultPreferences(preferences || {});
  try { localStorage.setItem(GUIDEVAULT_PREFERENCES_KEY, JSON.stringify(state.preferences)); } catch {}
  return state.preferences;
}

function setPreferencesStatus(message = '', tone = '') {
  const el = $('preferencesStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}

function renderPreferencesSettings() {
  const preferences = loadGuidevaultPreferences();
  if ($('preferenceUseColorscape')) $('preferenceUseColorscape').checked = !!preferences.useColorscape;
  if (document.body.classList.contains('detail-page-mode')) applyColorscapeToDetail(state.selected);
}

function setUseColorscapePreference(enabled) {
  saveGuidevaultPreferences({ ...(state.preferences || defaultGuidevaultPreferences()), useColorscape: !!enabled });
  setPreferencesStatus(enabled ? 'Colorscape enabled. Detail containers and background will use the selected cover color.' : 'Colorscape disabled. Theme gradients will be used.', enabled ? 'success' : 'info');
  if (document.body.classList.contains('detail-page-mode')) applyColorscapeToDetail(state.selected);
  else clearColorscapeDetailTheme();
}

function isColorscapeSupportedItem(item) {
  const kind = String(item?.kind || '').trim();
  return kind === 'Manual' || kind === 'Strategy Guide' || kind === 'Magazine';
}

function clearColorscapeDetailTheme() {
  state.colorscape.itemId = '';
  document.body.classList.remove('colorscape-active');
  ['--colorscape-rgb', '--colorscape-r', '--colorscape-g', '--colorscape-b'].forEach(name => document.body.style.removeProperty(name));
}

function applyColorscapeRgb(rgb, item) {
  const [r, g, b] = normalizeRgbTriplet(rgb);
  document.body.style.setProperty('--colorscape-rgb', `${r}, ${g}, ${b}`);
  document.body.style.setProperty('--colorscape-r', String(r));
  document.body.style.setProperty('--colorscape-g', String(g));
  document.body.style.setProperty('--colorscape-b', String(b));
  document.body.classList.add('colorscape-active');
  state.colorscape.itemId = String(item?.id || item?.Id || '');
}

function normalizeRgbTriplet(rgb, fallback = [88, 151, 255]) {
  const source = Array.isArray(rgb) && rgb.length >= 3 ? rgb : fallback;
  return source.slice(0, 3).map((v, i) => clampNumber(Math.round(v), 0, 255, fallback[i]));
}

function rgbToHsl(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b);
  let h = 0, s = 0;
  const l = (max + min) / 2;
  if (max !== min) {
    const d = max - min;
    s = l > .5 ? d / (2 - max - min) : d / (max + min);
    switch (max) {
      case r: h = (g - b) / d + (g < b ? 6 : 0); break;
      case g: h = (b - r) / d + 2; break;
      default: h = (r - g) / d + 4; break;
    }
    h /= 6;
  }
  return { h, s, l };
}

function hslToRgb(h, s, l) {
  let r, g, b;
  if (s === 0) {
    r = g = b = l;
  } else {
    const hue2rgb = (p, q, t) => {
      if (t < 0) t += 1;
      if (t > 1) t -= 1;
      if (t < 1 / 6) return p + (q - p) * 6 * t;
      if (t < 1 / 2) return q;
      if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
      return p;
    };
    const q = l < .5 ? l * (1 + s) : l + s - l * s;
    const p = 2 * l - q;
    r = hue2rgb(p, q, h + 1 / 3);
    g = hue2rgb(p, q, h);
    b = hue2rgb(p, q, h - 1 / 3);
  }
  return [Math.round(r * 255), Math.round(g * 255), Math.round(b * 255)];
}

function polishColorscapeColor(rgb) {
  const [r, g, b] = normalizeRgbTriplet(rgb);
  let { h, s, l } = rgbToHsl(r, g, b);
  // Keep the cover's identity, but lift very dull/dark scans enough to create a visible gradient.
  s = Math.max(.34, Math.min(.82, s * 1.18));
  l = Math.max(.32, Math.min(.58, l));
  return hslToRgb(h, s, l);
}

function sampleDominantCoverColor(img) {
  const canvas = document.createElement('canvas');
  const width = 48;
  const height = 64;
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  if (!ctx) return [88, 151, 255];
  ctx.drawImage(img, 0, 0, width, height);
  const data = ctx.getImageData(0, 0, width, height).data;
  const bins = new Map();
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const i = (y * width + x) * 4;
      const a = data[i + 3];
      if (a < 120) continue;
      const r = data[i], g = data[i + 1], b = data[i + 2];
      const { s, l } = rgbToHsl(r, g, b);
      if (l < .08 || l > .92) continue;
      if (s < .08 && (l < .2 || l > .82)) continue;
      // Bias toward central cover art instead of black borders/white page margins.
      const cx = Math.abs((x / width) - .5);
      const cy = Math.abs((y / height) - .5);
      const centerWeight = 1.18 - Math.min(.55, (cx + cy) * .55);
      const chromaWeight = .55 + s * 1.75;
      const lightWeight = .75 + (1 - Math.abs(l - .48)) * .55;
      const weight = centerWeight * chromaWeight * lightWeight;
      const key = `${r >> 4},${g >> 4},${b >> 4}`;
      const existing = bins.get(key) || { weight: 0, r: 0, g: 0, b: 0 };
      existing.weight += weight;
      existing.r += r * weight;
      existing.g += g * weight;
      existing.b += b * weight;
      bins.set(key, existing);
    }
  }
  let best = null;
  bins.forEach(bin => {
    if (!best || bin.weight > best.weight) best = bin;
  });
  if (!best || best.weight <= 0) return [88, 151, 255];
  return polishColorscapeColor([best.r / best.weight, best.g / best.weight, best.b / best.weight]);
}

function loadImageForColorscape(url) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.decoding = 'async';
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error('Cover image could not be loaded for Colorscape.'));
    img.src = url;
  });
}

async function getDominantCoverColor(url) {
  if (!url) return [88, 151, 255];
  state.colorscape.cache = state.colorscape.cache || {};
  if (state.colorscape.cache[url]) return state.colorscape.cache[url];
  try {
    const img = await loadImageForColorscape(url);
    const rgb = sampleDominantCoverColor(img);
    state.colorscape.cache[url] = rgb;
    return rgb;
  } catch (err) {
    console.warn('Colorscape sampling failed', err);
    return [88, 151, 255];
  }
}

async function applyColorscapeToDetail(item) {
  const preferences = state.preferences || loadGuidevaultPreferences();
  if (!preferences.useColorscape || !isColorscapeSupportedItem(item)) {
    clearColorscapeDetailTheme();
    return;
  }
  const itemId = String(item?.id || item?.Id || '');
  const url = coverUrl(item);
  const token = (state.colorscape.token || 0) + 1;
  state.colorscape.token = token;
  // Apply a safe color immediately so the page does not flash back to the theme gradient.
  if (!document.body.classList.contains('colorscape-active')) applyColorscapeRgb([88, 151, 255], item);
  const rgb = await getDominantCoverColor(url);
  if (state.colorscape.token !== token) return;
  if (!document.body.classList.contains('detail-page-mode')) return;
  if (String(state.selected?.id || state.selected?.Id || '') !== itemId) return;
  applyColorscapeRgb(rgb, item);
}
function cancelAccountEdit() {
  renderAccountProfile();
  setAccountStatus('Edit canceled.', 'info');
}
function logoutGuidevault() {
  stopDeviceHeartbeat();
  stopStableUpdatePolling();
  showLibraryScreen();
  showLoginScreen('Signed out of Guidevault.');
}
function initializeGuidevaultAuthAndApp() {
  loadReadingProfiles();
  loadGuidevaultPreferences();
  loadKeybinds();
  loadCustomizeSettings();
  loadOpdsSettings();
  syncOpdsSettingsFromServer(false);
  updateLoginPageMode();
  renderAccountProfile();
  showLoginScreen();
}


function fallbackSystemInfo() {
  return {
    appName: 'Guidevault',
    version: GUIDEVAULT_APP_VERSION,
    firstInstallVersion: GUIDEVAULT_APP_VERSION,
    firstInstallDate: new Date().toISOString(),
    installId: 'GV-LOCAL-PENDING',
    runtimeMode: 'Local self-hosted web app',
    supportedFiles: 'CBZ, CBR, PDF'
  };
}

function formatSystemDate(value) {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return date.toLocaleDateString(undefined, { year: 'numeric', month: 'numeric', day: 'numeric' });
}

function setSystemInfoStatus(message = '', tone = '') {
  const el = $('systemInfoStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}

function renderSystemInfo(info = state.systemInfo || fallbackSystemInfo()) {
  const data = info || fallbackSystemInfo();
  const setText = (id, value) => { const el = $(id); if (el) el.textContent = value || '—'; };
  setText('systemAppName', data.appName || 'Guidevault');
  setText('systemVersion', data.version || GUIDEVAULT_APP_VERSION);
  setText('systemFirstInstallVersion', data.firstInstallVersion || data.version || GUIDEVAULT_APP_VERSION);
  setText('systemFirstInstallDate', formatSystemDate(data.firstInstallDate));
  setText('systemInstallId', data.installId || '—');
  setText('systemRuntimeMode', data.runtimeMode || 'Local self-hosted web app');
  setText('systemSupportedFiles', data.supportedFiles || 'CBZ, CBR, PDF');
}

function formatDiagnosticBytes(value) {
  const n = Number(value || 0);
  if (!Number.isFinite(n) || n <= 0) return '0 MB';
  if (n >= 1024 ** 3) return `${(n / 1024 ** 3).toFixed(2)} GB`;
  if (n >= 1024 ** 2) return `${(n / 1024 ** 2).toFixed(1)} MB`;
  if (n >= 1024) return `${(n / 1024).toFixed(1)} KB`;
  return `${Math.round(n)} B`;
}

function renderSystemPerformance(data = state.performanceInfo) {
  const setText = (id, value) => { const el = $(id); if (el) el.textContent = value || '—'; };
  if (!data) {
    setText('systemWorkingSet', '—');
    setText('systemPrivateMemory', '—');
    setText('systemManagedMemory', '—');
    setText('systemGcMode', '—');
    setText('systemArchiveEntryCache', '—');
    setText('systemCoverDiskCache', '—');
    setText('systemCachedItems', '—');
    setText('systemActiveTasks', '—');
    setText('systemLastScan', '—');
    return;
  }
  setText('systemWorkingSet', formatDiagnosticBytes(data.process?.workingSetBytes));
  setText('systemPrivateMemory', formatDiagnosticBytes(data.process?.privateMemoryBytes));
  setText('systemManagedMemory', formatDiagnosticBytes(data.dotnet?.totalManagedMemoryBytes));
  setText('systemGcMode', data.dotnet?.isServerGc ? 'Server GC' : 'Workstation GC');
  setText('systemArchiveEntryCache', `${Number(data.archive?.imageEntryCacheCount || 0)} archive index entr${Number(data.archive?.imageEntryCacheCount || 0) === 1 ? 'y' : 'ies'} • ${Number(data.archive?.inFlightCoverReads || 0)} cover read(s)`);
  setText('systemCoverDiskCache', `${Number(data.archive?.diskCoverCacheFiles || 0)} file(s) • ${formatDiagnosticBytes(data.archive?.diskCoverCacheBytes)}`);
  setText('systemCachedItems', `${Number(data.library?.cachedItemCount || 0)} item(s)`);
  setText('systemActiveTasks', `${Number(data.tasks?.activeCount || 0)} active • ${Number(data.tasks?.recentCount || 0)} recent`);
  const last = data.library?.lastScan || {};
  const elapsed = Number(last.elapsedMs || 0);
  const elapsedText = elapsed > 0 ? `${(elapsed / 1000).toFixed(1)}s` : '—';
  setText('systemLastScan', last.message ? `${last.message} • ${elapsedText}` : 'No scan completed this session');
}

async function loadSystemPerformance() {
  try {
    const res = await fetch(`/api/system/performance?_=${Date.now()}`, { cache: 'no-store' });
    if (!res.ok) throw new Error(await res.text());
    state.performanceInfo = await res.json();
    renderSystemPerformance(state.performanceInfo);
    return state.performanceInfo;
  } catch (err) {
    console.warn('Performance diagnostics load failed', err);
    renderSystemPerformance(null);
    return null;
  }
}

async function trimGuidevaultMemory() {
  const btn = $('systemTrimMemory');
  if (btn) btn.disabled = true;
  setSystemInfoStatus('Trimming in-memory archive and cover request caches...', 'info');
  try {
    const res = await fetch('/api/system/performance/trim', { method: 'POST' });
    if (!res.ok) throw new Error(await res.text());
    await loadSystemPerformance();
    setSystemInfoStatus('Memory trim requested. Docker memory may take a little while to settle.', 'success');
  } catch (err) {
    console.warn('Memory trim failed', err);
    setSystemInfoStatus('Unable to trim memory from the app.', 'error');
  } finally {
    if (btn) btn.disabled = false;
  }
}

function renderUpdateNotification() {
  const update = state.updateCheck;
  const available = !!update?.updateAvailable;
  const btn = $('updateNotifyBtn');
  const badge = $('updateNotifyBadge');
  if (btn) {
    btn.classList.toggle('hidden', !available);
    btn.title = available ? `Guidevault ${update.latestVersion || 'stable'} is available` : 'Guidevault is up to date';
    btn.setAttribute('aria-label', btn.title);
  }
  if (badge) {
    badge.textContent = available ? '1' : '0';
    badge.classList.toggle('hidden', !available);
  }

  const notice = $('systemUpdateNotice');
  if (notice) {
    notice.classList.remove('hidden');
    notice.dataset.status = update?.status || '';
  }
  const setText = (id, value) => { const el = $(id); if (el) el.textContent = value || '—'; };
  setText('systemUpdateStatus', update?.message || 'Stable update notifications are not configured yet.');
  setText('systemUpdateCurrent', update?.currentVersion || GUIDEVAULT_APP_VERSION);
  setText('systemUpdateLatest', update?.latestVersion || '—');
  setText('systemUpdateImage', update?.latestImage || update?.currentImage || '—');
  const notes = $('systemUpdateNotes');
  if (notes) {
    const values = Array.isArray(update?.notes) ? update.notes.filter(Boolean) : [];
    notes.innerHTML = values.length ? values.map(note => `<li>${escapeHtml(note)}</li>`).join('') : '<li>No release notes were provided by the stable feed.</li>';
  }
  const link = $('systemUpdateLink');
  if (link) {
    const url = update?.releaseUrl || '';
    link.classList.toggle('hidden', !url);
    if (url) link.href = url;
  }
}

async function checkStableUpdates(force = false) {
  if (!force && state.updateCheck?.checkedAt && Date.now() - state.updateCheck.checkedAt < GUIDEVAULT_UPDATE_CHECK_MS) {
    renderUpdateNotification();
    return state.updateCheck;
  }
  try {
    const res = await fetch(`/api/system/update-check${force ? '?force=1' : ''}`, { cache: 'no-store' });
    if (!res.ok) throw new Error(await res.text());
    state.updateCheck = await res.json();
    state.updateCheck.checkedAt = Date.now();
    renderUpdateNotification();
    return state.updateCheck;
  } catch (err) {
    console.warn('Stable update check failed', err);
    state.updateCheck = { configured: true, status: 'error', message: 'Stable update check failed from the browser.', currentVersion: GUIDEVAULT_APP_VERSION, checkedAt: Date.now() };
    renderUpdateNotification();
    return state.updateCheck;
  }
}

function startStableUpdatePolling() {
  if (state.updateCheckTimer) return;
  checkStableUpdates(false);
  state.updateCheckTimer = window.setInterval(() => checkStableUpdates(false), GUIDEVAULT_UPDATE_CHECK_MS);
}
function stopStableUpdatePolling() {
  if (!state.updateCheckTimer) return;
  window.clearInterval(state.updateCheckTimer);
  state.updateCheckTimer = null;
}

async function loadSystemInfo(force = false) {
  if (!force && state.systemInfo) {
    renderSystemInfo(state.systemInfo);
    return state.systemInfo;
  }
  try {
    const res = await fetch(`/api/system/info?_=${Date.now()}`, { cache: 'no-store' });
    if (!res.ok) throw new Error(await res.text());
    state.systemInfo = await res.json();
    renderSystemInfo(state.systemInfo);
    loadSystemPerformance();
    checkStableUpdates(false);
    setSystemInfoStatus('');
    return state.systemInfo;
  } catch (err) {
    console.warn('System info load failed', err);
    state.systemInfo = fallbackSystemInfo();
    renderSystemInfo(state.systemInfo);
    renderUpdateNotification();
    setSystemInfoStatus('System info could not be loaded from the backend. Showing local fallback values.', 'error');
    return state.systemInfo;
  }
}

function defaultOpdsConnectionUrl() {
  try {
    const origin = window.location?.origin || '';
    return origin ? `${origin}/opds` : '/opds';
  } catch {
    return '/opds';
  }
}

function emptyOpdsSettingsState() {
  return {
    enabled: true,
    connectionUrl: defaultOpdsConnectionUrl(),
    selectedKeyId: '',
    keys: [],
    editingUrl: false,
    revealUrl: false,
    creatingKey: false
  };
}

function normalizeOpdsKey(value = {}, index = 0) {
  const id = String(value.id || `key-${Date.now()}-${index}`).trim();
  const name = String(value.name || `OPDS Key ${index + 1}`).trim() || `OPDS Key ${index + 1}`;
  const secret = String(value.secret || value.key || '').trim();
  const createdAt = value.createdAt || new Date().toISOString();
  return {
    id,
    name,
    secret,
    createdAt,
    rotatedAt: value.rotatedAt || null,
    expiresAt: value.expiresAt || null,
    lastAccessed: value.lastAccessed || null
  };
}

function normalizeOpdsSettings(value = {}) {
  const defaults = emptyOpdsSettingsState();
  const keys = Array.isArray(value.keys)
    ? value.keys.map((key, i) => normalizeOpdsKey(key, i)).filter(key => key.id && key.secret)
    : [];
  const selectedKeyId = keys.some(key => key.id === value.selectedKeyId)
    ? value.selectedKeyId
    : (keys[0]?.id || '');
  return {
    ...defaults,
    enabled: value.enabled === undefined ? defaults.enabled : value.enabled === true || String(value.enabled).toLowerCase() === 'true',
    connectionUrl: String(value.connectionUrl || defaults.connectionUrl).trim() || defaults.connectionUrl,
    selectedKeyId,
    keys
  };
}

function loadOpdsSettings() {
  try {
    const raw = localStorage.getItem(GUIDEVAULT_OPDS_SETTINGS_KEY);
    state.opds = normalizeOpdsSettings(raw ? JSON.parse(raw) : {});
  } catch {
    state.opds = emptyOpdsSettingsState();
  }
  return state.opds;
}

function saveOpdsSettings() {
  const normalized = normalizeOpdsSettings(state.opds || {});
  state.opds = { ...normalized, editingUrl: !!state.opds?.editingUrl, revealUrl: !!state.opds?.revealUrl, creatingKey: !!state.opds?.creatingKey };
  const persisted = {
    enabled: normalized.enabled,
    connectionUrl: normalized.connectionUrl,
    selectedKeyId: normalized.selectedKeyId,
    keys: normalized.keys
  };
  try { localStorage.setItem(GUIDEVAULT_OPDS_SETTINGS_KEY, JSON.stringify(persisted)); } catch {}
  return state.opds;
}

function setOpdsStatus(message = '', tone = '') {
  const el = $('opdsStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}

function preserveOpdsUiFlags(nextSettings) {
  const current = state.opds || {};
  return {
    ...normalizeOpdsSettings(nextSettings || {}),
    editingUrl: !!current.editingUrl,
    revealUrl: !!current.revealUrl,
    creatingKey: !!current.creatingKey
  };
}

async function syncOpdsSettingsFromServer(showStatus = false) {
  try {
    const response = await fetch('/api/opds/settings', { cache: 'no-store' });
    if (!response.ok) throw new Error(`OPDS settings request failed: ${response.status}`);
    const payload = await response.json();
    state.opds = preserveOpdsUiFlags(payload);
    saveOpdsSettings();
    renderOpdsSettings();
    if (showStatus) setOpdsStatus('OPDS backend settings loaded.', 'success');
    return state.opds;
  } catch (err) {
    console.warn('Unable to load OPDS backend settings', err);
    if (showStatus) setOpdsStatus('Unable to reach the OPDS backend settings API. Existing local display data is still shown.', 'error');
    renderOpdsSettings();
    return state.opds || loadOpdsSettings();
  }
}

async function saveOpdsServerSettings(partial = {}) {
  const settings = state.opds || loadOpdsSettings();
  const payload = {
    enabled: partial.enabled ?? settings.enabled ?? true,
    connectionUrl: partial.connectionUrl ?? settings.connectionUrl ?? defaultOpdsConnectionUrl(),
    selectedKeyId: partial.selectedKeyId ?? settings.selectedKeyId ?? ''
  };
  const response = await fetch('/api/opds/settings', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  if (!response.ok) throw new Error(`OPDS settings save failed: ${response.status}`);
  const saved = await response.json();
  state.opds = preserveOpdsUiFlags(saved);
  saveOpdsSettings();
  renderOpdsSettings();
  return state.opds;
}

async function postOpdsKeyAction(url, options = {}) {
  const response = await fetch(url, options);
  if (!response.ok) {
    let message = `OPDS key request failed: ${response.status}`;
    try {
      const payload = await response.json();
      if (payload?.error) message = payload.error;
    } catch {}
    throw new Error(message);
  }
  const saved = await response.json();
  state.opds = preserveOpdsUiFlags(saved);
  saveOpdsSettings();
  renderOpdsSettings();
  return state.opds;
}

function generateOpdsSecret() {
  const bytes = new Uint8Array(32);
  if (window.crypto?.getRandomValues) window.crypto.getRandomValues(bytes);
  else for (let i = 0; i < bytes.length; i++) bytes[i] = Math.floor(Math.random() * 256);
  let binary = '';
  bytes.forEach(byte => binary += String.fromCharCode(byte));
  return `GV-OPDS-${btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')}`;
}

function formatOpdsDate(value, fallback = 'Never') {
  if (!value) return fallback;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return fallback;
  return date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

function getSelectedOpdsKey() {
  const settings = state.opds || loadOpdsSettings();
  return settings.keys.find(key => key.id === settings.selectedKeyId) || settings.keys[0] || null;
}

function buildOpdsClientUrl(key = getSelectedOpdsKey()) {
  const settings = state.opds || loadOpdsSettings();
  const base = String(settings.connectionUrl || defaultOpdsConnectionUrl()).trim() || defaultOpdsConnectionUrl();
  if (!key?.secret) return base;
  const joiner = base.includes('?') ? '&' : '?';
  return `${base}${joiner}auth=${encodeURIComponent(key.secret)}`;
}

function maskOpdsKey(secret) {
  const length = Math.max(14, Math.min(String(secret || '').length, 24));
  return '•'.repeat(length);
}

function escapeForAttribute(value) {
  return String(value ?? '').replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function renderOpdsSettings() {
  const settings = state.opds || loadOpdsSettings();
  if (!$('settingsOpdsPanel')) return;

  const editing = !!settings.editingUrl;
  const reveal = !!settings.revealUrl || editing;
  if ($('opdsEnabledToggle')) $('opdsEnabledToggle').checked = settings.enabled !== false;
  const connection = $('opdsConnectionUrl');
  if (connection) {
    connection.value = editing ? settings.connectionUrl : buildOpdsClientUrl();
    connection.type = reveal ? 'text' : 'password';
    connection.readOnly = !editing;
  }
  if ($('opdsEditUrl')) $('opdsEditUrl').classList.toggle('hidden', editing);
  if ($('opdsSaveUrl')) $('opdsSaveUrl').classList.toggle('hidden', !editing);
  if ($('opdsCancelUrl')) $('opdsCancelUrl').classList.toggle('hidden', !editing);
  if ($('opdsRevealUrl')) $('opdsRevealUrl').textContent = reveal && !editing ? 'Hide' : 'Reveal';

  const selector = $('opdsUrlKeySelect');
  if (selector) {
    selector.innerHTML = settings.keys.length
      ? settings.keys.map(key => `<option value="${escapeForAttribute(key.id)}">${escapeHtml(key.name)}</option>`).join('')
      : '<option value="">No key generated</option>';
    selector.value = settings.selectedKeyId || settings.keys[0]?.id || '';
    selector.disabled = settings.keys.length === 0;
  }

  const inline = $('opdsNewKeyInline');
  if (inline) inline.classList.toggle('hidden', !settings.creatingKey);
  if ($('opdsNewKeyName') && settings.creatingKey && !$('opdsNewKeyName').value) $('opdsNewKeyName').value = `OPDS ${settings.keys.length + 1}`;

  const body = $('opdsKeyTableBody');
  if (body) {
    body.innerHTML = settings.keys.length
      ? settings.keys.map(key => `
        <tr data-key-id="${escapeForAttribute(key.id)}">
          <td><strong>${escapeHtml(key.name)}</strong></td>
          <td><span class="opds-masked-key">${maskOpdsKey(key.secret)}</span><button class="opds-inline-copy" type="button" data-opds-action="copy-key" title="Copy key">⧉</button></td>
          <td>${escapeHtml(key.expiresAt ? formatOpdsDate(key.expiresAt) : 'Never')}</td>
          <td>${escapeHtml(formatOpdsDate(key.lastAccessed))}</td>
          <td class="opds-actions-cell"><button class="opds-action-button" type="button" data-opds-action="rotate" title="Rotate key">⟳</button><button class="opds-action-button danger" type="button" data-opds-action="delete" title="Delete key">ðŸ—‘</button></td>
        </tr>`).join('')
      : '<tr><td colspan="5" class="opds-empty-row">No authorization keys yet. Select + New to generate one.</td></tr>';
  }
  if ($('opdsKeyCount')) $('opdsKeyCount').textContent = `${settings.keys.length} total`;
}


function setServerSettingsStatus(message = '', tone = '') {
  const el = $('serverSettingsStatus');
  if (el) { el.textContent = message || ''; el.dataset.tone = tone || ''; }
}
function setMediaSettingsStatus(message = '', tone = '') {
  const el = $('mediaSettingsStatus');
  if (el) { el.textContent = message || ''; el.dataset.tone = tone || ''; }
}
function setEmailSettingsStatus(message = '', tone = '') {
  const el = $('emailSettingsStatus');
  if (el) { el.textContent = message || ''; el.dataset.tone = tone || ''; }
}
function setUsersStatus(message = '', tone = '') {
  const el = $('usersStatus');
  if (el) { el.textContent = message || ''; el.dataset.tone = tone || ''; }
}
function setTasksSettingsStatus(message = '', tone = '') {
  const el = $('tasksSettingsStatus');
  if (el) { el.textContent = message || ''; el.dataset.tone = tone || ''; }
}
function defaultServerSettings() {
  return { hostName: window.location?.origin || 'http://localhost:5478', baseUrl: '/', ipAddresses: '', port: 5478, loggingLevel: 'Information', backupDirectory: 'data/backups', bookmarksDirectory: 'data/bookmarks' };
}
function normalizeServerSettings(value = {}) {
  const defaults = defaultServerSettings();
  return {
    hostName: String(value.hostName || defaults.hostName).trim(),
    baseUrl: String(value.baseUrl || defaults.baseUrl).trim() || '/',
    ipAddresses: String(value.ipAddresses || '').trim(),
    port: Number(value.port || defaults.port) || defaults.port,
    loggingLevel: String(value.loggingLevel || defaults.loggingLevel).trim(),
    backupDirectory: String(value.backupDirectory || defaults.backupDirectory).trim(),
    bookmarksDirectory: String(value.bookmarksDirectory || defaults.bookmarksDirectory).trim()
  };
}
async function loadServerSettings(showStatus = false) {
  try {
    const res = await fetch('/api/server/settings', { cache: 'no-store' });
    if (!res.ok) throw new Error(`Server settings request failed: ${res.status}`);
    state.serverSettings = normalizeServerSettings(await res.json());
    renderServerSettings();
    if (showStatus) setServerSettingsStatus('General server settings loaded.', 'success');
  } catch (err) {
    console.warn('Unable to load server settings', err);
    state.serverSettings = state.serverSettings || defaultServerSettings();
    renderServerSettings();
    if (showStatus) setServerSettingsStatus('Unable to load server settings from the backend.', 'error');
  }
  return state.serverSettings;
}
function renderServerSettings() {
  const settings = normalizeServerSettings(state.serverSettings || defaultServerSettings());
  if ($('serverHostName')) $('serverHostName').value = settings.hostName;
  if ($('serverBaseUrl')) $('serverBaseUrl').value = settings.baseUrl;
  if ($('serverIpAddresses')) $('serverIpAddresses').value = settings.ipAddresses;
  if ($('serverPort')) $('serverPort').value = settings.port;
  if ($('serverBackupDirectory')) $('serverBackupDirectory').value = settings.backupDirectory;
  if ($('serverLoggingLevel')) $('serverLoggingLevel').value = settings.loggingLevel;
  if ($('mediaBookmarksDirectory')) $('mediaBookmarksDirectory').value = settings.bookmarksDirectory;
}
function collectServerSettings() {
  const existing = normalizeServerSettings(state.serverSettings || defaultServerSettings());
  return normalizeServerSettings({
    ...existing,
    hostName: $('serverHostName')?.value ?? existing.hostName,
    baseUrl: $('serverBaseUrl')?.value ?? existing.baseUrl,
    ipAddresses: $('serverIpAddresses')?.value ?? existing.ipAddresses,
    port: $('serverPort')?.value ?? existing.port,
    loggingLevel: $('serverLoggingLevel')?.value ?? existing.loggingLevel,
    backupDirectory: $('serverBackupDirectory')?.value ?? existing.backupDirectory,
    bookmarksDirectory: $('mediaBookmarksDirectory')?.value ?? existing.bookmarksDirectory
  });
}
async function saveServerSettings(source = 'general') {
  const payload = collectServerSettings();
  try {
    const res = await fetch('/api/server/settings', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
    if (!res.ok) throw new Error(`Save failed: ${res.status}`);
    state.serverSettings = normalizeServerSettings(await res.json());
    renderServerSettings();
    const msg = source === 'media' ? 'Media settings saved.' : 'General server settings saved. Restart Guidevault if you changed listener values.';
    source === 'media' ? setMediaSettingsStatus(msg, 'success') : setServerSettingsStatus(msg, 'success');
  } catch (err) {
    console.error('Unable to save server settings', err);
    const msg = `Unable to save settings: ${err?.message || err}`;
    source === 'media' ? setMediaSettingsStatus(msg, 'error') : setServerSettingsStatus(msg, 'error');
  }
}
function resetServerDefaults() {
  state.serverSettings = defaultServerSettings();
  renderServerSettings();
  setServerSettingsStatus('Defaults restored locally. Choose Save to write them.', 'info');
}
async function createServerBackup() {
  try {
    setServerSettingsStatus('Creating library backup...', 'info');
    const res = await fetch('/api/server/backup', { method: 'POST', cache: 'no-store' });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data?.error || `Backup failed: ${res.status}`);
    const message = `Backup created: ${data.fileName || 'library backup'} (${fmtBytes(Number(data.sizeBytes || 0))}).`;
    setServerSettingsStatus(message, 'success');
    setTasksSettingsStatus(message, 'success');
  } catch (err) {
    const message = `Backup failed: ${err?.message || err}`;
    setServerSettingsStatus(message, 'error');
    setTasksSettingsStatus(message, 'error');
  }
}
function normalizeEmailSettings(value = {}) {
  const selectedTemplateId = String(value.selectedTemplateId || 'guidevault-default').trim() || 'guidevault-default';
  const preset = getEmailTemplatePreset(selectedTemplateId === 'custom-upload' ? 'guidevault-default' : selectedTemplateId);
  const templateBody = String(value.templateBody ?? '').trim() ? String(value.templateBody) : preset.body;
  const requestedProvider = String(value.provider || '').trim();
  const provider = EMAIL_PROVIDER_PRESETS[requestedProvider] ? requestedProvider : (String(value.host || value.username || '').trim() ? 'smtp' : 'transactional-resend');
  const providerPreset = EMAIL_PROVIDER_PRESETS[provider] || EMAIL_PROVIDER_PRESETS['transactional-resend'];
  return {
    provider,
    hostName: String(value.hostName || '').trim(),
    senderAddress: String(value.senderAddress || '').trim(),
    displayName: String(value.displayName || 'Guidevault').trim(),
    host: String(value.host || '').trim(),
    port: Number(value.port || 587) || 587,
    useSsl: value.useSsl !== false,
    username: String(value.username || '').trim(),
    password: String(value.password || '').trim(),
    apiKey: String(value.apiKey || '').trim(),
    apiEndpoint: String(value.apiEndpoint || providerPreset.endpoint || '').trim(),
    sizeLimitMb: Number(value.sizeLimitMb || 25) || 25,
    customizedTemplates: value.customizedTemplates === true,
    selectedTemplateId,
    templateName: String(value.templateName || preset.name || 'Guidevault Invite').trim() || 'Guidevault Invite',
    templateSubject: String(value.templateSubject || preset.subject || 'Guidevault invite').trim() || 'Guidevault invite',
    templateBody
  };
}
function getEmailTemplatePreset(id) {
  return EMAIL_TEMPLATE_PRESETS.find(t => t.id === id) || EMAIL_TEMPLATE_PRESETS[0];
}
function sampleEmailTemplateBody(body = '') {
  const raw = String(body ?? '').trim() ? String(body) : EMAIL_TEMPLATE_PRESETS[0].body;
  return raw
    .replace(/{{appName}}/gi, 'Guidevault')
    .replace(/{{displayName}}/gi, 'Reader')
    .replace(/{{email}}/gi, 'reader@example.com')
    .replace(/{{inviteUrl}}/gi, state.serverSettings?.hostName || window.location.origin || 'http://localhost:5478')
    .replace(/{{permissions}}/gi, 'Login, Bookmark, Read Only');
}
async function loadEmailSettings(showStatus = false) {
  try {
    const res = await fetch('/api/email/settings', { cache: 'no-store' });
    if (!res.ok) throw new Error(`Email settings request failed: ${res.status}`);
    state.emailSettings = normalizeEmailSettings(await res.json());
    renderEmailSettings();
    if (showStatus) setEmailSettingsStatus('Email settings loaded.', 'success');
  } catch (err) {
    console.warn('Unable to load email settings', err);
    state.emailSettings = state.emailSettings || normalizeEmailSettings();
    renderEmailSettings();
    if (showStatus) setEmailSettingsStatus('Unable to load email settings from the backend.', 'error');
  }
}
function renderEmailTemplatePresetOptions() {
  const select = $('emailTemplatePreset');
  if (!select) return;
  const current = state.emailSettings?.selectedTemplateId || select.value || 'guidevault-default';
  select.innerHTML = EMAIL_TEMPLATE_PRESETS.map(t => `<option value="${escapeForAttribute(t.id)}">${escapeHtml(t.name)}</option>`).join('') + '<option value="custom-upload">Custom Uploaded Template</option>';
  select.value = current === 'custom-upload' || EMAIL_TEMPLATE_PRESETS.some(t => t.id === current) ? current : 'custom-upload';
}

function syncEmailProviderUi() {
  const provider = $('emailProvider')?.value || state.emailSettings?.provider || 'transactional-resend';
  const preset = EMAIL_PROVIDER_PRESETS[provider] || EMAIL_PROVIDER_PRESETS['transactional-resend'];
  const smtpMode = provider === 'smtp';
  document.querySelectorAll('.email-smtp-field').forEach(el => el.classList.toggle('hidden', !smtpMode));
  document.querySelectorAll('.email-api-field').forEach(el => el.classList.toggle('hidden', smtpMode));
  setText('emailProviderSummary', preset.summary || preset.label || 'Email');
  setText('emailProviderSummaryText', preset.label || 'Email provider');
  const help = $('emailProviderHelp');
  if (help) help.innerHTML = `<strong>${escapeHtml(preset.label || 'Email provider')}</strong><span>${escapeHtml(preset.help || '')}</span>`;
  const endpoint = $('emailApiEndpoint');
  if (endpoint && !smtpMode && (!endpoint.value.trim() || Object.values(EMAIL_PROVIDER_PRESETS).some(p => p.endpoint && p.endpoint === endpoint.value.trim()))) {
    endpoint.value = preset.endpoint || endpoint.value;
  }
}
function renderEmailSettings() {
  const settings = normalizeEmailSettings(state.emailSettings || {});
  renderEmailTemplatePresetOptions();
  if ($('emailProvider')) $('emailProvider').value = settings.provider || 'transactional-resend';
  if ($('emailHostName')) $('emailHostName').value = settings.hostName || (state.serverSettings?.hostName || '');
  if ($('emailSenderAddress')) $('emailSenderAddress').value = settings.senderAddress;
  if ($('emailDisplayName')) $('emailDisplayName').value = settings.displayName;
  if ($('emailHost')) $('emailHost').value = settings.host;
  if ($('emailPort')) $('emailPort').value = settings.port;
  if ($('emailUseSsl')) $('emailUseSsl').checked = settings.useSsl;
  if ($('emailUsername')) $('emailUsername').value = settings.username;
  if ($('emailPassword')) $('emailPassword').value = settings.password;
  if ($('emailApiKey')) $('emailApiKey').value = settings.apiKey;
  if ($('emailApiEndpoint')) $('emailApiEndpoint').value = settings.apiEndpoint;
  if ($('emailSizeLimitMb')) $('emailSizeLimitMb').value = settings.sizeLimitMb;
  if ($('emailCustomizedTemplates')) $('emailCustomizedTemplates').checked = settings.customizedTemplates;
  if ($('emailTemplatePreset')) $('emailTemplatePreset').value = settings.selectedTemplateId || 'guidevault-default';
  if ($('emailTemplateName')) $('emailTemplateName').value = settings.templateName;
  if ($('emailTemplateSubject')) $('emailTemplateSubject').value = settings.templateSubject;
  if ($('emailTemplateBody')) $('emailTemplateBody').value = settings.templateBody;
  syncEmailProviderUi();
  syncEmailTemplatePreview();
}
function collectEmailSettings() {
  return normalizeEmailSettings({
    provider: $('emailProvider')?.value || 'transactional-resend',
    hostName: $('emailHostName')?.value,
    senderAddress: $('emailSenderAddress')?.value,
    displayName: $('emailDisplayName')?.value,
    host: $('emailHost')?.value,
    port: $('emailPort')?.value,
    useSsl: !!$('emailUseSsl')?.checked,
    username: $('emailUsername')?.value,
    password: $('emailPassword')?.value,
    apiKey: $('emailApiKey')?.value,
    apiEndpoint: $('emailApiEndpoint')?.value,
    sizeLimitMb: $('emailSizeLimitMb')?.value,
    customizedTemplates: !!$('emailCustomizedTemplates')?.checked,
    selectedTemplateId: $('emailTemplatePreset')?.value || 'guidevault-default',
    templateName: $('emailTemplateName')?.value,
    templateSubject: $('emailTemplateSubject')?.value,
    templateBody: $('emailTemplateBody')?.value
  });
}
function handleEmailTemplatePresetChange() {
  const id = $('emailTemplatePreset')?.value || 'guidevault-default';
  const preset = getEmailTemplatePreset(id);
  if (id !== 'custom-upload') {
    if ($('emailTemplateName')) $('emailTemplateName').value = preset.name;
    if ($('emailTemplateSubject')) $('emailTemplateSubject').value = preset.subject;
    if ($('emailTemplateBody')) $('emailTemplateBody').value = preset.body;
    if ($('emailCustomizedTemplates')) $('emailCustomizedTemplates').checked = false;
  } else {
    if ($('emailCustomizedTemplates')) $('emailCustomizedTemplates').checked = true;
  }
  syncEmailTemplatePreview();
}
function sanitizeEmailPreviewHtml(rawHtml = '') {
  const source = String(rawHtml || '');
  try {
    const doc = new DOMParser().parseFromString(source, 'text/html');
    doc.querySelectorAll('script, iframe, object, embed, frame, frameset, meta[http-equiv], link[rel=preload], link[rel=prefetch]').forEach(node => node.remove());
    doc.querySelectorAll('*').forEach(node => {
      Array.from(node.attributes || []).forEach(attr => {
        const name = String(attr.name || '').toLowerCase();
        const value = String(attr.value || '');
        if (name.startsWith('on')) node.removeAttribute(attr.name);
        if ((name === 'href' || name === 'src' || name === 'xlink:href') && /^\s*javascript:/i.test(value)) node.removeAttribute(attr.name);
        if (name === 'srcdoc') node.removeAttribute(attr.name);
      });
    });
    const bodyHtml = doc.body?.innerHTML || '';
    return bodyHtml.trim() ? bodyHtml : escapeHtml(source);
  } catch {
    return escapeHtml(source);
  }
}
function renderEmailPreviewIntoPanel(preview, body, isHtml) {
  preview.classList.toggle('plain-text-preview', !isHtml);
  preview.replaceChildren();
  const host = document.createElement('div');
  host.className = 'email-template-preview-host';
  preview.appendChild(host);
  const content = isHtml ? sanitizeEmailPreviewHtml(body) : `<pre>${escapeHtml(body)}</pre>`;
  const fallback = '<div class="email-preview-empty"><strong>No template body to preview.</strong><span>Choose a built-in template, type template content, or upload an HTML/text template.</span></div>';
  try {
    const root = host.attachShadow ? host.attachShadow({ mode: 'open' }) : null;
    if (root) {
      root.innerHTML = `<style>
        :host{all:initial;display:block;font-family:Arial,system-ui,sans-serif;color:#111827;line-height:1.55;}
        *,*::before,*::after{box-sizing:border-box;}
        h1,h2,h3{color:#111827;margin:0 0 12px;line-height:1.2;font-family:Arial,system-ui,sans-serif;}
        p{margin:0 0 12px;color:#1f2937;font-family:Arial,system-ui,sans-serif;}
        a{color:#2563eb;}
        pre{margin:0;white-space:pre-wrap;font-family:Arial,system-ui,sans-serif;line-height:1.45;color:#111827;}
        .email-preview-empty{display:grid;gap:6px;border:1px dashed #94a3b8;border-radius:12px;padding:16px;background:#f8fafc;color:#334155;font-family:Arial,system-ui,sans-serif;}
        .email-preview-empty strong{color:#0f172a;}
      </style><div class="email-preview-rendered">${content.trim() ? content : fallback}</div>`;
      const text = root.textContent || '';
      if (!text.trim() && !root.querySelector('img,svg,table')) root.querySelector('.email-preview-rendered').innerHTML = fallback;
      return;
    }
  } catch (err) {
    console.warn('Shadow preview render failed; using direct render.', err);
  }
  host.innerHTML = content.trim() ? content : fallback;
}
function syncEmailTemplatePreview() {
  const subject = String($('emailTemplateSubject')?.value || 'Guidevault invite').trim() || 'Guidevault invite';
  const body = sampleEmailTemplateBody($('emailTemplateBody')?.value || EMAIL_TEMPLATE_PRESETS[0].body);
  const isHtml = /<([a-z][\w:-]*)(\s|>|\/)/i.test(body);
  setText('emailPreviewSubject', subject);
  setText('emailPreviewStatus', isHtml ? 'Rendered HTML preview' : 'Rendered plain-text preview');
  const preview = $('emailTemplatePreviewFrame');
  if (preview) renderEmailPreviewIntoPanel(preview, body, isHtml);
}
async function uploadEmailTemplateFile(file) {
  if (!file) return;
  try {
    const text = await file.text();
    if ($('emailTemplatePreset')) $('emailTemplatePreset').value = 'custom-upload';
    if ($('emailCustomizedTemplates')) $('emailCustomizedTemplates').checked = true;
    if ($('emailTemplateName')) $('emailTemplateName').value = file.name.replace(/\.(html?|txt)$/i, '') || 'Custom Template';
    if ($('emailTemplateSubject') && !$('emailTemplateSubject').value.trim()) $('emailTemplateSubject').value = 'Guidevault invite';
    if ($('emailTemplateBody')) $('emailTemplateBody').value = text;
    syncEmailTemplatePreview();
    setEmailSettingsStatus('Custom template loaded locally. Save Email Settings to keep it.', 'info');
  } catch (err) {
    setEmailSettingsStatus(`Template upload failed: ${err?.message || err}`, 'error');
  }
}
async function saveEmailSettings() {
  try {
    const res = await fetch('/api/email/settings', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(collectEmailSettings()) });
    if (!res.ok) throw new Error(`Email save failed: ${res.status}`);
    state.emailSettings = normalizeEmailSettings(await res.json());
    renderEmailSettings();
    setEmailSettingsStatus('Email settings saved.', 'success');
  } catch (err) {
    console.error('Unable to save email settings', err);
    setEmailSettingsStatus(`Unable to save email settings: ${err?.message || err}`, 'error');
  }
}
async function testEmailSettings() {
  const to = String($('emailTestTo')?.value || '').trim();
  if (!to) { setEmailSettingsStatus('Enter a recipient for the test email.', 'error'); return; }
  try {
    setEmailSettingsStatus('Sending test email...', 'info');
    const payload = { to, subject: $('emailTemplateSubject')?.value || 'Guidevault test email', body: $('emailTemplateBody')?.value || EMAIL_TEMPLATE_PRESETS[0].body };
    const res = await fetch('/api/email/test', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data?.error || `Test email failed: ${res.status}`);
    state.emailHistory = Array.isArray(data.history) ? data.history : state.emailHistory;
    setEmailSettingsStatus('Test email sent and logged.', 'success');
  } catch (err) {
    setEmailSettingsStatus(`Test email failed: ${err?.message || err}`, 'error');
  }
}
async function loadEmailHistory(showStatus = false) {
  try {
    const res = await fetch('/api/email/history', { cache: 'no-store' });
    if (!res.ok) throw new Error(`Email history request failed: ${res.status}`);
    state.emailHistory = await res.json();
    renderEmailHistory();
    if (showStatus) setEmailHistoryStatus('Email history refreshed.', 'success');
  } catch (err) {
    console.warn('Unable to load email history', err);
    state.emailHistory = Array.isArray(state.emailHistory) ? state.emailHistory : [];
    renderEmailHistory();
    if (showStatus) setEmailHistoryStatus('Unable to load email history from the backend.', 'error');
  }
}
function setEmailHistoryStatus(message = '', tone = '') {
  const el = $('emailHistoryStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}
function renderEmailHistory() {
  const host = $('emailHistoryList');
  if (!host) return;
  const items = Array.isArray(state.emailHistory) ? state.emailHistory : [];
  host.innerHTML = items.length ? items.map(item => {
    const sentAt = item.sentAt ? new Date(item.sentAt).toLocaleString() : 'Unknown time';
    const status = item.status || 'Logged';
    return `<article class="settings-card email-history-row" data-status="${escapeForAttribute(status.toLowerCase())}">
      <div class="email-history-main"><strong>${escapeHtml(item.subject || 'Guidevault email')}</strong><span>${escapeHtml(item.type || 'Email')} • ${escapeHtml(item.to || '—')}</span><small>${escapeHtml(sentAt)}</small></div>
      <div class="email-history-meta"><span>${escapeHtml(status)}</span><em>${escapeHtml(item.templateName || 'Guidevault Invite')}</em></div>
      ${item.message ? `<p class="sub email-history-message">${escapeHtml(item.message)}</p>` : ''}
    </article>`;
  }).join('') : '<article class="settings-card"><p class="sub">No email has been sent or attempted yet.</p></article>';
}
async function loadUsersSettings(showStatus = false) {
  try {
    const res = await fetch('/api/users', { cache: 'no-store' });
    if (!res.ok) throw new Error(`Users request failed: ${res.status}`);
    const data = await res.json();
    state.usersSettings = { users: Array.isArray(data.users) ? data.users : [], libraries: Array.isArray(data.libraries) ? data.libraries : [], permissions: Array.isArray(data.permissions) ? data.permissions : [] };
    renderUsersSettings();
    if (showStatus) setUsersStatus('Users refreshed.', 'success');
  } catch (err) {
    console.warn('Unable to load users', err);
    renderUsersSettings();
    if (showStatus) setUsersStatus('Unable to load users from the backend.', 'error');
  }
}
function libraryNameForInvite(lib, index = 0) {
  return String(lib?.name || lib?.Name || lib?.libraryPath || lib?.LibraryPath || lib?.path || lib?.Path || `Library ${index + 1}`).trim();
}
function renderUsersSettings() {
  const data = state.usersSettings || { users: [], libraries: [], permissions: [] };
  const libHost = $('inviteLibrariesList');
  if (libHost) {
    const libraries = data.libraries?.length ? data.libraries : (state.libraries || []);
    libHost.innerHTML = libraries.map((lib, index) => {
      const name = libraryNameForInvite(lib, index);
      return `<label class="inline-check"><input type="checkbox" value="${escapeForAttribute(name)}" checked /> <span>${escapeHtml(name)}</span></label>`;
    }).join('') || '<p class="sub">No libraries configured yet.</p>';
  }
  const permHost = $('invitePermissionsList');
  if (permHost) {
    const permissions = data.permissions?.length ? data.permissions : ['Login', 'Bookmark', 'Download', 'Read Only'];
    permHost.innerHTML = permissions.map(name => `<label class="inline-check"><input type="checkbox" value="${escapeForAttribute(name)}" ${['Login','Bookmark','Read Only'].includes(name) ? 'checked' : ''} /> <span>${escapeHtml(name)}</span></label>`).join('');
  }
  const usersHost = $('usersList');
  if (usersHost) {
    usersHost.innerHTML = (data.users || []).map(user => {
      const initials = String(user.displayName || user.email || 'GV').trim().split(/\s+/).slice(0,2).map(x => x[0] || '').join('').toUpperCase() || 'GV';
      const libraries = (user.libraries || []).join(', ') || 'No library access';
      const permissions = (user.permissions || []).join(', ') || 'No permissions';
      return `<article class="settings-card user-card refined-user-card">
        <div class="user-avatar-badge">${escapeHtml(initials)}</div>
        <div class="user-card-main"><h3>${escapeHtml(user.displayName || user.email || 'Invited user')}</h3><p class="sub">${escapeHtml(user.email || '')}</p><div class="user-chip-list left"><span>${escapeHtml(user.role || 'Reader')}</span><span>${escapeHtml(user.status || 'Invited')}</span><span>${escapeHtml(user.ageRatingRestriction || 'No Restriction')}</span></div></div>
        <div class="user-access-summary"><strong>Libraries</strong><span>${escapeHtml(libraries)}</span><strong>Permissions</strong><span>${escapeHtml(permissions)}</span></div>
      </article>`;
    }).join('') || '<article class="settings-card"><p class="sub">No invited users yet.</p></article>';
  }
}
async function inviteUser() {
  const email = String($('inviteEmail')?.value || '').trim();
  if (!email) { setUsersStatus('Enter an email address first.', 'error'); return; }
  const checkedValues = host => [...(host?.querySelectorAll('input[type="checkbox"]:checked') || [])].map(x => x.value).filter(Boolean);
  const payload = {
    email,
    displayName: $('inviteDisplayName')?.value || '',
    role: $('inviteRole')?.value || 'Reader',
    libraries: checkedValues($('inviteLibrariesList')),
    permissions: checkedValues($('invitePermissionsList')),
    ageRatingRestriction: $('inviteAgeRestriction')?.value || 'No Restriction',
    includeUnknowns: !!$('inviteIncludeUnknowns')?.checked
  };
  try {
    const res = await fetch('/api/users/invite', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data?.error || `Invite failed: ${res.status}`);
    const sent = data.sent === true || data.emailSent === true;
    setUsersStatus(sent ? 'Invite saved and email sent.' : (data.emailMessage || 'Invite saved. Email was not sent because SMTP is not fully configured or sending failed.'), sent ? 'success' : 'info');
    ['inviteEmail','inviteDisplayName'].forEach(id => { if ($(id)) $(id).value = ''; });
    await loadUsersSettings(false);
  } catch (err) {
    console.error('Unable to invite user', err);
    setUsersStatus(`Invite failed: ${err?.message || err}`, 'error');
  }
}
function normalizeTaskSettings(value = {}) {
  return { libraryScan: String(value.libraryScan || 'Daily'), guidevaultBackup: String(value.guidevaultBackup || 'Daily'), cleanup: String(value.cleanup || 'Daily'), readingListSync: String(value.readingListSync || 'Custom (0 4 * * *)') };
}
async function loadTaskSettings(showStatus = false) {
  try {
    const res = await fetch('/api/tasks/settings', { cache: 'no-store' });
    if (!res.ok) throw new Error(`Task settings request failed: ${res.status}`);
    state.taskSettings = normalizeTaskSettings(await res.json());
    renderTaskSettings();
    if (showStatus) setTasksSettingsStatus('Task settings loaded.', 'success');
  } catch (err) {
    console.warn('Unable to load task settings', err);
    state.taskSettings = state.taskSettings || normalizeTaskSettings();
    renderTaskSettings();
    if (showStatus) setTasksSettingsStatus('Unable to load task settings from the backend.', 'error');
  }
}
function renderTaskSettings() {
  const settings = normalizeTaskSettings(state.taskSettings || {});
  if ($('taskLibraryScan')) $('taskLibraryScan').value = settings.libraryScan;
  if ($('taskGuidevaultBackup')) $('taskGuidevaultBackup').value = settings.guidevaultBackup;
  if ($('taskCleanup')) $('taskCleanup').value = settings.cleanup;
  if ($('taskReadingListSync')) $('taskReadingListSync').value = settings.readingListSync;
}
function collectTaskSettings() {
  return normalizeTaskSettings({ libraryScan: $('taskLibraryScan')?.value, guidevaultBackup: $('taskGuidevaultBackup')?.value, cleanup: $('taskCleanup')?.value, readingListSync: $('taskReadingListSync')?.value });
}
async function saveTaskSettings() {
  try {
    const res = await fetch('/api/tasks/settings', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(collectTaskSettings()) });
    if (!res.ok) throw new Error(`Task save failed: ${res.status}`);
    state.taskSettings = normalizeTaskSettings(await res.json());
    renderTaskSettings();
    setTasksSettingsStatus('Recurring task settings saved.', 'success');
  } catch (err) {
    console.error('Unable to save task settings', err);
    setTasksSettingsStatus(`Unable to save task settings: ${err?.message || err}`, 'error');
  }
}

function beginOpdsUrlEdit() {
  state.opds = state.opds || loadOpdsSettings();
  state.opds.editingUrl = true;
  state.opds.revealUrl = true;
  setOpdsStatus('Editing OPDS URL.', 'info');
  renderOpdsSettings();
  requestAnimationFrame(() => $('opdsConnectionUrl')?.focus?.());
}

async function saveOpdsUrl() {
  state.opds = state.opds || loadOpdsSettings();
  const input = $('opdsConnectionUrl');
  const value = String(input?.value || '').trim();
  if (!value) { setOpdsStatus('Enter an OPDS URL before saving.', 'error'); return; }
  const sanitized = value.replace(/[?&]auth=[^&]+/i, '').replace(/[?&]key=[^&]+/i, '').replace(/[?&]token=[^&]+/i, '');
  try {
    state.opds.connectionUrl = sanitized;
    state.opds.editingUrl = false;
    state.opds.revealUrl = false;
    await saveOpdsServerSettings({ connectionUrl: sanitized });
    setOpdsStatus('OPDS URL saved to the backend and masked.', 'success');
  } catch (err) {
    console.warn('Unable to save OPDS URL', err);
    setOpdsStatus('Unable to save the OPDS URL to the backend.', 'error');
    renderOpdsSettings();
  }
}

function cancelOpdsUrlEdit() {
  state.opds = state.opds || loadOpdsSettings();
  state.opds.editingUrl = false;
  state.opds.revealUrl = false;
  setOpdsStatus('OPDS URL edit canceled.', 'info');
  renderOpdsSettings();
}

function toggleOpdsRevealUrl() {
  state.opds = state.opds || loadOpdsSettings();
  if (state.opds.editingUrl) return;
  state.opds.revealUrl = !state.opds.revealUrl;
  renderOpdsSettings();
}

async function copyTextToClipboard(text, successMessage = 'Copied.') {
  try {
    await navigator.clipboard.writeText(text);
    setOpdsStatus(successMessage, 'success');
  } catch {
    const temp = document.createElement('textarea');
    temp.value = text;
    temp.setAttribute('readonly', '');
    temp.style.position = 'fixed';
    temp.style.opacity = '0';
    document.body.appendChild(temp);
    temp.select();
    try { document.execCommand('copy'); setOpdsStatus(successMessage, 'success'); }
    catch { setOpdsStatus('Copy failed. Reveal and copy manually.', 'error'); }
    finally { temp.remove(); }
  }
}

function beginOpdsNewKey() {
  state.opds = state.opds || loadOpdsSettings();
  state.opds.creatingKey = true;
  setOpdsStatus('Name the new authorization key, then generate it.', 'info');
  renderOpdsSettings();
  requestAnimationFrame(() => $('opdsNewKeyName')?.focus?.());
}

function cancelOpdsNewKey() {
  state.opds = state.opds || loadOpdsSettings();
  state.opds.creatingKey = false;
  if ($('opdsNewKeyName')) $('opdsNewKeyName').value = '';
  setOpdsStatus('New key canceled.', 'info');
  renderOpdsSettings();
}

async function createOpdsKey() {
  state.opds = state.opds || loadOpdsSettings();
  const name = String($('opdsNewKeyName')?.value || '').trim() || `OPDS ${state.opds.keys.length + 1}`;
  try {
    state.opds.creatingKey = false;
    state.opds.revealUrl = false;
    await postOpdsKeyAction('/api/opds/keys', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name })
    });
    if ($('opdsNewKeyName')) $('opdsNewKeyName').value = '';
    const key = getSelectedOpdsKey();
    setOpdsStatus(`Generated server-backed key "${key?.name || name}". Copy the OPDS URL into a reader client.`, 'success');
  } catch (err) {
    console.warn('Unable to create OPDS key', err);
    state.opds.creatingKey = true;
    setOpdsStatus('Unable to generate an OPDS key from the backend.', 'error');
    renderOpdsSettings();
  }
}

async function rotateOpdsKey(id) {
  state.opds = state.opds || loadOpdsSettings();
  const key = state.opds.keys.find(k => k.id === id);
  if (!key) return;
  try {
    await postOpdsKeyAction(`/api/opds/keys/${encodeURIComponent(id)}/rotate`, { method: 'POST' });
    const rotated = getSelectedOpdsKey();
    setOpdsStatus(`Rotated key "${rotated?.name || key.name}". Existing clients using the old key will need the new value.`, 'success');
  } catch (err) {
    console.warn('Unable to rotate OPDS key', err);
    setOpdsStatus('Unable to rotate the OPDS key on the backend.', 'error');
    renderOpdsSettings();
  }
}

async function deleteOpdsKey(id) {
  state.opds = state.opds || loadOpdsSettings();
  const key = state.opds.keys.find(k => k.id === id);
  if (!key) return;
  if (!confirm(`Delete the OPDS authorization key "${key.name}"?`)) return;
  try {
    await postOpdsKeyAction(`/api/opds/keys/${encodeURIComponent(id)}`, { method: 'DELETE' });
    setOpdsStatus(`Deleted key "${key.name}".`, 'success');
  } catch (err) {
    console.warn('Unable to delete OPDS key', err);
    setOpdsStatus('Unable to delete the OPDS key from the backend.', 'error');
    renderOpdsSettings();
  }
}

function handleOpdsTableAction(e) {
  const actionButton = e.target.closest?.('[data-opds-action]');
  if (!actionButton) return;
  e.preventDefault();
  const row = actionButton.closest('tr[data-key-id]');
  const id = row?.dataset?.keyId || '';
  const key = (state.opds || loadOpdsSettings()).keys.find(k => k.id === id);
  if (!key) return;
  const action = actionButton.dataset.opdsAction;
  if (action === 'copy-key') copyTextToClipboard(key.secret, `Copied key "${key.name}".`);
  if (action === 'rotate') rotateOpdsKey(id);
  if (action === 'delete') deleteOpdsKey(id);
}

async function handleOpdsKeySelection() {
  state.opds = state.opds || loadOpdsSettings();
  state.opds.selectedKeyId = $('opdsUrlKeySelect')?.value || '';
  try {
    await saveOpdsServerSettings({ selectedKeyId: state.opds.selectedKeyId });
  } catch (err) {
    console.warn('Unable to save selected OPDS key', err);
    saveOpdsSettings();
    setOpdsStatus('Unable to save selected OPDS key to the backend.', 'error');
    renderOpdsSettings();
  }
}


function emptyDeviceHistoryState() {
  return { emailDevices: [], clientDevices: [], generatedAt: null, addingEmail: false, editingEmailId: '', editingClientId: '', clientMenuId: '' };
}

function normalizeDeviceSnapshot(payload = {}) {
  const current = state.devices || emptyDeviceHistoryState();
  return {
    ...current,
    emailDevices: Array.isArray(payload.emailDevices) ? payload.emailDevices : [],
    clientDevices: Array.isArray(payload.clientDevices) ? payload.clientDevices : [],
    generatedAt: payload.generatedAt || new Date().toISOString(),
    addingEmail: !!current.addingEmail,
    editingEmailId: current.editingEmailId || '',
    editingClientId: current.editingClientId || '',
    clientMenuId: current.clientMenuId || ''
  };
}

function setDeviceStatus(message = '', tone = '') {
  const el = $('deviceStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}

function detectClientBrowser() {
  const ua = navigator.userAgent || '';
  const matchers = [
    ['Edge', /Edg\/(\d+(?:\.\d+)?)/i],
    ['Chrome', /Chrome\/(\d+(?:\.\d+)?)/i],
    ['Firefox', /Firefox\/(\d+(?:\.\d+)?)/i],
    ['Safari', /Version\/(\d+(?:\.\d+)?).*Safari/i]
  ];
  for (const [name, pattern] of matchers) {
    const match = ua.match(pattern);
    if (match) return { name, version: match[1] || '' };
  }
  return { name: 'Browser', version: '' };
}

function detectClientPlatform() {
  const ua = navigator.userAgent || '';
  if (/android/i.test(ua)) return 'Android';
  if (/iphone|ipad|ipod/i.test(ua)) return 'iOS';
  if (/windows/i.test(ua)) return 'Windows';
  if (/macintosh|mac os/i.test(ua)) return 'macOS';
  if (/linux/i.test(ua)) return 'Linux';
  return navigator.platform || 'Unknown';
}

function currentDeviceScreenText() {
  const width = window.screen?.width || window.innerWidth || 0;
  const height = window.screen?.height || window.innerHeight || 0;
  const orientation = width && height ? (width >= height ? 'landscape' : 'portrait') : '';
  return width && height ? `${width}Ã—${height}${orientation ? ` (${orientation})` : ''}` : '';
}

function buildDeviceHeartbeatPayload() {
  const browser = detectClientBrowser();
  const platform = detectClientPlatform();
  const profile = state.auth.profile || readLoginProfile() || {};
  return {
    displayName: `${browser.name} on ${platform}`,
    username: profile.username || '',
    email: profile.email || '',
    browserName: browser.name,
    browserVersion: browser.version,
    platform,
    screen: currentDeviceScreenText(),
    appVersion: GUIDEVAULT_APP_VERSION,
    userAgent: navigator.userAgent || '',
    lastPath: location.pathname || '/'
  };
}

async function sendDeviceHeartbeat({ refresh = false } = {}) {
  if (!state.auth.authenticated) return null;
  try {
    const response = await fetch('/api/devices/heartbeat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(buildDeviceHeartbeatPayload())
    });
    if (!response.ok) throw new Error(`Device heartbeat failed: ${response.status}`);
    const payload = await response.json();
    if (payload?.snapshot) {
      state.devices = normalizeDeviceSnapshot(payload.snapshot);
      if (!$('settingsDevicesPanel')?.classList.contains('hidden') || !$('settingsInsightDevicesPanel')?.classList.contains('hidden')) renderDeviceHistory();
    } else if (refresh) {
      await loadDeviceHistory(false);
    }
    return payload;
  } catch (err) {
    console.warn('Unable to send device heartbeat', err);
    return null;
  }
}

function startDeviceHeartbeat() {
  stopDeviceHeartbeat();
  sendDeviceHeartbeat({ refresh: true });
  state.deviceHeartbeatTimer = setInterval(() => sendDeviceHeartbeat(), GUIDEVAULT_DEVICE_HEARTBEAT_MS);
}

function stopDeviceHeartbeat() {
  if (state.deviceHeartbeatTimer) {
    clearInterval(state.deviceHeartbeatTimer);
    state.deviceHeartbeatTimer = null;
  }
}

async function loadDeviceHistory(showStatus = false) {
  try {
    const response = await fetch('/api/devices', { cache: 'no-store' });
    if (!response.ok) throw new Error(`Device history failed: ${response.status}`);
    const payload = await response.json();
    state.devices = normalizeDeviceSnapshot(payload);
    renderDeviceHistory();
    if (showStatus) setDeviceStatus('Device history refreshed.', 'success');
    return state.devices;
  } catch (err) {
    console.warn('Unable to load device history', err);
    if (showStatus) setDeviceStatus('Unable to load device history from the backend.', 'error');
    renderDeviceHistory();
    return state.devices || emptyDeviceHistoryState();
  }
}

function formatDeviceDate(value, fallback = '—') {
  if (!value) return fallback;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return fallback;
  return date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

function formatDeviceRelative(value, fallback = 'Never') {
  if (!value) return fallback;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return fallback;
  const diff = Date.now() - date.getTime();
  if (diff < 60_000) return 'just now';
  const minutes = Math.round(diff / 60_000);
  if (minutes < 60) return `${minutes} minute${minutes === 1 ? '' : 's'} ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 48) return `${hours} hour${hours === 1 ? '' : 's'} ago`;
  const days = Math.round(hours / 24);
  if (days < 60) return `${days} day${days === 1 ? '' : 's'} ago`;
  return formatDeviceDate(value, fallback);
}

function shortBrowserVersion(version = '') {
  const parts = String(version || '').split('.').filter(Boolean);
  return parts.length ? parts[0] + (parts[1] ? `.${parts[1]}` : '') : '';
}


function deviceIcon(name) {
  const icons = {
    desktop: '<rect x="3" y="4" width="18" height="12" rx="2"></rect><path d="M8 20h8"></path><path d="M12 16v4"></path>',
    mobile: '<rect x="7" y="2" width="10" height="20" rx="2"></rect><path d="M11 18h2"></path>',
    tablet: '<rect x="5" y="3" width="14" height="18" rx="2"></rect><path d="M11 18h2"></path>',
    browser: '<circle cx="12" cy="12" r="9"></circle><path d="M3 12h18"></path><path d="M12 3c2.4 2.6 3.6 5.6 3.6 9S14.4 18.4 12 21"></path><path d="M12 3c-2.4 2.6-3.6 5.6-3.6 9S9.6 18.4 12 21"></path>',
    platform: '<path d="M12 3 3 7.5 12 12l9-4.5L12 3Z"></path><path d="m3 12 9 4.5 9-4.5"></path><path d="m3 16.5 9 4.5 9-4.5"></path>',
    screen: '<rect x="3" y="4" width="18" height="12" rx="2"></rect><path d="M7 20h10"></path><path d="M12 16v4"></path>',
    version: '<circle cx="6" cy="6" r="3"></circle><circle cx="18" cy="18" r="3"></circle><path d="M8.5 8.5 15.5 15.5"></path><path d="M14 6h4v4"></path>',
    calendar: '<rect x="3" y="5" width="18" height="16" rx="2"></rect><path d="M16 3v4"></path><path d="M8 3v4"></path><path d="M3 10h18"></path>',
    clock: '<circle cx="12" cy="12" r="9"></circle><path d="M12 7v5l3 2"></path>',
    opds: '<path d="M5 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z"></path><path d="M4 5c8.3 0 15 6.7 15 15"></path><path d="M4 11c5 0 9 4 9 9"></path>'
  };
  const body = icons[name] || icons.desktop;
  return `<svg class="gv-device-icon gv-device-icon-${name}" viewBox="0 0 24 24" aria-hidden="true" focusable="false">${body}</svg>`;
}

function clientDeviceTitleIcon(device = {}) {
  const platform = String(device.platform || '').toLowerCase();
  const clientType = String(device.clientType || '').toLowerCase();
  if (platform.includes('android') || platform.includes('ios') || platform.includes('iphone') || platform.includes('ipad') || platform.includes('mobile')) return deviceIcon('mobile');
  if (clientType.includes('opds')) return deviceIcon('opds');
  return deviceIcon('desktop');
}

function deviceFact(iconName, label, value) {
  return `<div class="device-card-fact">${deviceIcon(iconName)}<span>${label}: <b>${escapeHtml(value || '—')}</b></span></div>`;
}

function renderDeviceHistory() {
  const devices = state.devices || emptyDeviceHistoryState();
  renderEmailDeviceTable(devices.emailDevices || []);
  renderClientDeviceCards(devices.clientDevices || []);
  renderInsightDeviceDashboard(devices.clientDevices || []);
  const editor = $('deviceEmailEditor');
  if (editor) editor.classList.toggle('hidden', !devices.addingEmail);
}

function renderEmailDeviceTable(emailDevices = []) {
  const body = $('deviceEmailTableBody');
  if (body) {
    body.innerHTML = emailDevices.length
      ? emailDevices.map(device => `
        <tr data-email-device-id="${escapeForAttribute(device.id || '')}">
          <td><strong>${escapeHtml(device.name || 'Email Device')}</strong></td>
          <td>${escapeHtml(device.email || '—')}</td>
          <td>${escapeHtml(device.platform || 'Email')}</td>
          <td><button class="device-table-action danger" type="button" data-device-email-action="delete">ðŸ—‘</button></td>
        </tr>`).join('')
      : '<tr><td colspan="4" class="device-empty-row">No data to display</td></tr>';
  }
  if ($('deviceEmailCount')) $('deviceEmailCount').textContent = `${emailDevices.length} total`;
}

function clientDeviceBadges(device) {
  const badges = [];
  if (device.isActive) badges.push('<span class="device-badge active">Active</span>');
  if (device.clientType) badges.push(`<span class="device-badge ${String(device.clientType).toLowerCase().includes('opds') ? 'opds' : 'web'}">${escapeHtml(device.clientType)}</span>`);
  if (device.authKeyName) badges.push('<span class="device-badge auth">Auth Key</span>');
  return badges.join('');
}

function renderClientDeviceCards(clientDevices = []) {
  renderClientDeviceCardsInto('deviceClientGrid', clientDevices, {
    manageable: true,
    emptyText: 'No connected client history yet. Open Guidevault in this browser or connect through OPDS to create the first entry.'
  });
}

function renderClientDeviceCardsInto(gridId, clientDevices = [], options = {}) {
  const grid = $(gridId);
  if (!grid) return;
  const devices = state.devices || emptyDeviceHistoryState();
  const manageable = options.manageable !== false;
  const emptyText = options.emptyText || 'No connected client history yet.';
  if (!clientDevices.length) {
    grid.innerHTML = `<article class="settings-card device-client-empty">${escapeHtml(emptyText)}</article>`;
    return;
  }
  grid.innerHTML = clientDevices.map(device => {
    const id = device.id || '';
    const isEditing = manageable && devices.editingClientId === id;
    const menuOpen = manageable && devices.clientMenuId === id && !isEditing;
    const browserVersion = shortBrowserVersion(device.browserVersion || '');
    const browserText = device.browserName ? `${device.browserName}${browserVersion ? ` ${browserVersion}` : ''}` : '—';
    const userText = device.username || device.authKeyName || device.email || 'local';
    const displayName = device.displayName || 'Guidevault Client';
    return `
      <article class="device-client-card" data-client-device-id="${escapeForAttribute(id)}">
        ${manageable && !isEditing ? `
          <div class="device-card-menu">
            <button class="device-card-menu-button" type="button" data-device-client-action="toggle-menu" title="Device options" aria-label="Device options">⋮</button>
            <div class="device-card-menu-popover${menuOpen ? ' open' : ''}">
              <button type="button" data-device-client-action="edit-name">Edit name</button>
              <button class="danger" type="button" data-device-client-action="delete">Delete</button>
            </div>
          </div>` : ''}
        <div class="device-card-title">
          ${clientDeviceTitleIcon(device)}
          ${isEditing
            ? `<input class="device-client-name-input" data-device-client-name-input value="${escapeForAttribute(displayName)}" maxlength="80" />`
            : `<strong>${escapeHtml(displayName)}</strong>`}
        </div>
        ${isEditing ? `
          <div class="device-card-edit-actions">
            <button class="primary" type="button" data-device-client-action="save-name">Save</button>
            <button class="ghost" type="button" data-device-client-action="cancel-edit">Cancel</button>
          </div>` : ''}
        <div class="device-card-badges">${clientDeviceBadges(device)}<span class="device-card-user">${escapeHtml(userText)}</span></div>
        <div class="device-card-facts">
          ${deviceFact('browser', 'Browser', browserText)}
          ${deviceFact('platform', 'Platform', device.platform || 'Unknown')}
          ${deviceFact('screen', 'Screen', device.screen || '—')}
          ${deviceFact('version', 'App Version', device.appVersion || '—')}
        </div>
        <div class="device-card-footer">
          <span class="device-card-footer-item">${deviceIcon('calendar')} First Seen: ${escapeHtml(formatDeviceDate(device.firstSeen))}</span>
          <span class="device-card-footer-item">${deviceIcon('clock')} Last Seen: ${escapeHtml(formatDeviceRelative(device.lastSeen))}</span>
        </div>
      </article>`;
  }).join('');
}

function isDeviceSeenThisMonth(device = {}) {
  const value = device.lastSeen || device.firstSeen || device.updatedAt || device.createdAt;
  if (!value) return false;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return false;
  const now = new Date();
  return date.getFullYear() === now.getFullYear() && date.getMonth() === now.getMonth();
}

function clientTypeLabel(device = {}) {
  const type = String(device.clientType || '').trim();
  if (/opds/i.test(type)) return 'OPDS Client';
  if (/web/i.test(type)) return 'Web App';
  if (type) return type;
  if (device.authKeyName) return 'OPDS Client';
  return 'Other Client';
}

function deviceClassLabel(device = {}) {
  const platform = String(device.platform || '').toLowerCase();
  const ua = String(device.userAgent || '').toLowerCase();
  const screen = String(device.screen || '');
  if (/ipad|tablet|silk|kindle|playbook/.test(platform) || /ipad|tablet|silk|kindle|playbook/.test(ua)) return 'Tablet';
  if (/iphone|ipod|android|ios|mobile/.test(platform) || /iphone|ipod|android|mobile/.test(ua)) return 'Mobile';
  const match = screen.match(/(\d+)\s*[Ã—x]\s*(\d+)/i);
  if (match) {
    const width = Number(match[1]);
    const height = Number(match[2]);
    const shortest = Math.min(width || 0, height || 0);
    const longest = Math.max(width || 0, height || 0);
    if (shortest && longest && shortest <= 900 && longest <= 1400) return 'Tablet';
  }
  return 'Desktop';
}

function aggregateCounts(items = [], labeler = () => 'Other') {
  const counts = new Map();
  items.forEach(item => {
    const label = labeler(item) || 'Other';
    counts.set(label, (counts.get(label) || 0) + 1);
  });
  return Array.from(counts, ([label, value]) => ({ label, value })).sort((a, b) => b.value - a.value || a.label.localeCompare(b.label));
}

function countDevicesByLabel(items = [], labeler = () => 'Other', expectedLabel = '') {
  return items.reduce((count, item) => count + (labeler(item) === expectedLabel ? 1 : 0), 0);
}

function deviceMonthLabel() {
  return new Date().toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
}

function renderInsightDeviceSummary(monthDevices = []) {
  const el = $('insightDeviceSummary');
  if (!el) return;
  const activeNow = monthDevices.filter(device => !!device.isActive).length;
  const webApps = countDevicesByLabel(monthDevices, clientTypeLabel, 'Web App');
  const opdsClients = countDevicesByLabel(monthDevices, clientTypeLabel, 'OPDS Client');
  const mobileOrTablet = monthDevices.filter(device => {
    const label = deviceClassLabel(device);
    return label === 'Mobile' || label === 'Tablet';
  }).length;
  const cards = [
    { label: 'Seen this month', value: monthDevices.length, note: deviceMonthLabel(), icon: 'screen' },
    { label: 'Active now', value: activeNow, note: 'Seen in the last few minutes', icon: 'clock' },
    { label: 'Web app sessions', value: webApps, note: 'Browser-based clients', icon: 'browser' },
    { label: 'OPDS readers', value: opdsClients, note: `${mobileOrTablet} mobile/tablet`, icon: 'opds' }
  ];
  el.innerHTML = cards.map(card => `
    <article class="insight-device-summary-card">
      <div class="insight-device-summary-icon">${deviceIcon(card.icon)}</div>
      <div>
        <strong>${escapeHtml(card.value)}</strong>
        <span>${escapeHtml(card.label)}</span>
        <small>${escapeHtml(card.note)}</small>
      </div>
    </article>`).join('');
}

function semiDonutSegmentPath(cx, cy, radius, startDeg, endDeg) {
  const start = polarPoint(cx, cy, radius, startDeg);
  const end = polarPoint(cx, cy, radius, endDeg);
  const largeArc = Math.abs(endDeg - startDeg) > 180 ? 1 : 0;
  return `M ${start.x.toFixed(2)} ${start.y.toFixed(2)} A ${radius} ${radius} 0 ${largeArc} 1 ${end.x.toFixed(2)} ${end.y.toFixed(2)}`;
}

function polarPoint(cx, cy, radius, degrees) {
  const radians = degrees * Math.PI / 180;
  return { x: cx + radius * Math.cos(radians), y: cy + radius * Math.sin(radians) };
}

function renderSemiDonutChart(containerId, segments = [], options = {}) {
  const el = $(containerId);
  if (!el) return;
  const colors = ['#5f7bd9', '#8dcc73', '#ffc857', '#1ccff0', '#9a68ff', '#f06c9b'];
  const total = segments.reduce((sum, segment) => sum + Math.max(0, Number(segment.value) || 0), 0);
  if (!total) {
    el.innerHTML = '<div class="insight-chart-empty">No device activity recorded this month.</div>';
    return;
  }
  let cursor = 180;
  const paths = segments.map((segment, index) => {
    const fraction = Math.max(0, Number(segment.value) || 0) / total;
    const end = Math.min(360, cursor + fraction * 180);
    const path = semiDonutSegmentPath(160, 154, 88, cursor, end);
    cursor = end;
    return `<path class="insight-donut-segment" d="${path}" style="--segment-color:${colors[index % colors.length]}"></path>`;
  }).join('');
  const legend = segments.map((segment, index) => `
    <span class="insight-chart-legend-item"><i style="--legend-color:${colors[index % colors.length]}"></i>${escapeHtml(segment.label)}</span>`).join('');
  const label = options.totalLabel || 'devices';
  el.innerHTML = `
    <div class="insight-chart-legend">${legend}</div>
    <div class="insight-donut-wrap">
      <svg class="insight-donut-svg" viewBox="0 0 320 190" role="img" aria-label="${escapeForAttribute(options.ariaLabel || 'Device chart')}">
        <path class="insight-donut-track" d="${semiDonutSegmentPath(160, 154, 88, 180, 360)}"></path>
        ${paths}
      </svg>
      <div class="insight-donut-total"><strong>${total}</strong><span>${escapeHtml(label)}</span></div>
    </div>`;
}

function renderInsightDeviceDashboard(clientDevices = []) {
  const monthDevices = clientDevices.filter(isDeviceSeenThisMonth);
  renderInsightDeviceSummary(monthDevices);
  renderSemiDonutChart('insightClientTypeChart', aggregateCounts(monthDevices, clientTypeLabel), {
    totalLabel: monthDevices.length === 1 ? 'client' : 'clients',
    ariaLabel: 'Client devices by type this month'
  });
  renderSemiDonutChart('insightDeviceClassChart', aggregateCounts(monthDevices, deviceClassLabel), {
    totalLabel: monthDevices.length === 1 ? 'device' : 'devices',
    ariaLabel: 'Mobile versus desktop devices this month'
  });
  renderClientDeviceCardsInto('insightDeviceClientGrid', monthDevices, {
    manageable: false,
    emptyText: 'No client devices have connected this month yet.'
  });
}

function beginEmailDeviceAdd() {
  state.devices = state.devices || emptyDeviceHistoryState();
  state.devices.addingEmail = true;
  state.devices.editingEmailId = '';
  ['deviceEmailName', 'deviceEmailAddress', 'deviceEmailPlatform'].forEach(id => { if ($(id)) $(id).value = ''; });
  setDeviceStatus('Add an email-based device that can receive files.', 'info');
  renderDeviceHistory();
  requestAnimationFrame(() => $('deviceEmailName')?.focus?.());
}

function cancelEmailDeviceAdd() {
  state.devices = state.devices || emptyDeviceHistoryState();
  state.devices.addingEmail = false;
  state.devices.editingEmailId = '';
  setDeviceStatus('Email device canceled.', 'info');
  renderDeviceHistory();
}

async function saveEmailDevice() {
  const name = String($('deviceEmailName')?.value || '').trim();
  const email = String($('deviceEmailAddress')?.value || '').trim();
  const platform = String($('deviceEmailPlatform')?.value || '').trim() || 'Email';
  if (!name || !email) { setDeviceStatus('Name and email are required.', 'error'); return; }
  try {
    const response = await fetch('/api/devices/email', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, email, platform })
    });
    if (!response.ok) throw new Error(`Email device save failed: ${response.status}`);
    const snapshot = await response.json();
    state.devices = normalizeDeviceSnapshot(snapshot);
    state.devices.addingEmail = false;
    renderDeviceHistory();
    setDeviceStatus(`Saved email device "${name}".`, 'success');
  } catch (err) {
    console.warn('Unable to save email device', err);
    setDeviceStatus('Unable to save the email device.', 'error');
  }
}

async function deleteEmailDevice(id) {
  if (!id) return;
  if (!confirm('Delete this email-based device?')) return;
  try {
    const response = await fetch(`/api/devices/email/${encodeURIComponent(id)}`, { method: 'DELETE' });
    if (!response.ok) throw new Error(`Email device delete failed: ${response.status}`);
    const snapshot = await response.json();
    state.devices = normalizeDeviceSnapshot(snapshot);
    renderDeviceHistory();
    setDeviceStatus('Email device deleted.', 'success');
  } catch (err) {
    console.warn('Unable to delete email device', err);
    setDeviceStatus('Unable to delete the email device.', 'error');
  }
}


function beginClientDeviceRename(id) {
  if (!id) return;
  state.devices = state.devices || emptyDeviceHistoryState();
  state.devices.editingClientId = id;
  state.devices.clientMenuId = '';
  renderDeviceHistory();
  requestAnimationFrame(() => {
    const input = document.querySelector(`[data-client-device-id="${CSS.escape(id)}"] [data-device-client-name-input]`);
    input?.focus?.();
    input?.select?.();
  });
}

function cancelClientDeviceRename() {
  state.devices = state.devices || emptyDeviceHistoryState();
  state.devices.editingClientId = '';
  state.devices.clientMenuId = '';
  renderDeviceHistory();
}

async function saveClientDeviceName(id) {
  if (!id) return;
  const input = document.querySelector(`[data-client-device-id="${CSS.escape(id)}"] [data-device-client-name-input]`);
  const displayName = String(input?.value || '').trim();
  if (!displayName) { setDeviceStatus('Device name is required.', 'error'); input?.focus?.(); return; }
  try {
    const response = await fetch(`/api/devices/clients/${encodeURIComponent(id)}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ displayName })
    });
    if (!response.ok) throw new Error(`Client device rename failed: ${response.status}`);
    const snapshot = await response.json();
    state.devices = normalizeDeviceSnapshot(snapshot);
    state.devices.editingClientId = '';
    state.devices.clientMenuId = '';
    renderDeviceHistory();
    setDeviceStatus('Client device name updated.', 'success');
  } catch (err) {
    console.warn('Unable to update client device name', err);
    setDeviceStatus('Unable to update the client device name.', 'error');
  }
}

async function deleteClientDevice(id) {
  if (!id) return;
  if (!confirm('Remove this client device history entry?')) return;
  try {
    const response = await fetch(`/api/devices/clients/${encodeURIComponent(id)}`, { method: 'DELETE' });
    if (!response.ok) throw new Error(`Client device delete failed: ${response.status}`);
    const snapshot = await response.json();
    state.devices = normalizeDeviceSnapshot(snapshot);
    renderDeviceHistory();
    setDeviceStatus('Client device history entry removed.', 'success');
  } catch (err) {
    console.warn('Unable to delete client device', err);
    setDeviceStatus('Unable to remove the client device history entry.', 'error');
  }
}

async function clearStaleClientDevices() {
  if (!confirm('Clear client device history entries not seen in 30+ days?')) return;
  try {
    const response = await fetch('/api/devices/clients/clear-stale?days=30', { method: 'POST' });
    if (!response.ok) throw new Error(`Clear stale devices failed: ${response.status}`);
    const snapshot = await response.json();
    state.devices = normalizeDeviceSnapshot(snapshot);
    renderDeviceHistory();
    setDeviceStatus('Cleared stale client device history.', 'success');
  } catch (err) {
    console.warn('Unable to clear stale client device history', err);
    setDeviceStatus('Unable to clear stale client device history.', 'error');
  }
}

function handleDeviceEmailTableAction(e) {
  const button = e.target.closest?.('[data-device-email-action]');
  if (!button) return;
  e.preventDefault();
  const row = button.closest('tr[data-email-device-id]');
  const id = row?.dataset?.emailDeviceId || '';
  if (button.dataset.deviceEmailAction === 'delete') deleteEmailDevice(id);
}

function handleDeviceClientGridAction(e) {
  const button = e.target.closest?.('[data-device-client-action]');
  if (!button) return;
  e.preventDefault();
  const card = button.closest('[data-client-device-id]');
  const id = card?.dataset?.clientDeviceId || '';
  const action = button.dataset.deviceClientAction;
  state.devices = state.devices || emptyDeviceHistoryState();
  if (action === 'toggle-menu') {
    state.devices.clientMenuId = state.devices.clientMenuId === id ? '' : id;
    renderDeviceHistory();
    return;
  }
  if (action === 'edit-name') { beginClientDeviceRename(id); return; }
  if (action === 'save-name') { saveClientDeviceName(id); return; }
  if (action === 'cancel-edit') { cancelClientDeviceRename(); return; }
  if (action === 'delete') { state.devices.clientMenuId = ''; deleteClientDevice(id); }
}

function handleDeviceClientGridKeydown(e) {
  const input = e.target.closest?.('[data-device-client-name-input]');
  if (!input) return;
  const card = input.closest('[data-client-device-id]');
  const id = card?.dataset?.clientDeviceId || '';
  if (e.key === 'Enter') {
    e.preventDefault();
    saveClientDeviceName(id);
  }
  if (e.key === 'Escape') {
    e.preventDefault();
    cancelClientDeviceRename();
  }
}


function clampNumber(value, min, max, fallback) {
  const n = Number(value);
  if (!Number.isFinite(n)) return fallback;
  return Math.max(min, Math.min(max, n));
}
function defaultReaderShading() {
  return { bindingEnabled: true, bindingDepth: 68, outerEnabled: true, outerDepth: 62 };
}
function normalizeReaderShading(value = {}) {
  const defaults = defaultReaderShading();
  return {
    bindingEnabled: value.bindingEnabled !== false,
    bindingDepth: clampNumber(value.bindingDepth, 0, 100, defaults.bindingDepth),
    outerEnabled: value.outerEnabled !== false,
    outerDepth: clampNumber(value.outerDepth, 0, 100, defaults.outerDepth)
  };
}
function loadReaderShading() {
  try {
    return normalizeReaderShading(JSON.parse(localStorage.getItem(READER_SHADING_KEY) || '{}'));
  } catch {
    return normalizeReaderShading();
  }
}
function saveReaderShading() {
  try { localStorage.setItem(READER_SHADING_KEY, JSON.stringify(normalizeReaderShading(state.reader.shading || {}))); } catch {}
}
function setCssVar(el, name, value) {
  if (el) el.style.setProperty(name, value);
}
function alpha(value) {
  return String(Math.max(0, Math.min(1, Number(value) || 0)).toFixed(3));
}

function readerBackgroundDisplayName(name) {
  const key = String(name || '').toLowerCase();
  const known = {
    'adventuremap.png': 'Adventure Map',
    'bathroom.png': 'Bathroom',
    'gamerbedroom.png': 'Gamer Bedroom',
    'librarydesk.png': 'Library Desk',
    'livingroom.png': 'Living Room',
    'schoolbus.png': 'School Bus',
    'spacehud.png': 'Space HUD',
    'warriorhud.png': 'Warrior HUD',
    'wood.png': 'Wood'
  };
  if (known[key]) return known[key];
  const base = String(name || '').replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').trim();
  if (!base) return 'Default Gradient';
  return base.replace(/\b\w/g, c => c.toUpperCase());
}

function fallbackReaderBackgrounds() {
  const names = [
    'adventuremap.png',
    'bathroom.png',
    'gamerbedroom.png',
    'librarydesk.png',
    'livingroom.png',
    'schoolbus.png',
    'spacehud.png',
    'warriorhud.png',
    'wood.png'
  ];
  return names.map(name => ({
    name,
    displayName: readerBackgroundDisplayName(name),
    url: `/assets/backgrounds/${encodeURIComponent(name)}`
  }));
}
function mergeReaderBackgroundLists(primary = [], fallback = []) {
  const seen = new Set();
  return [...primary, ...fallback]
    .map(bg => ({
      name: String(bg?.name || '').trim(),
      displayName: String(bg?.displayName || readerBackgroundDisplayName(bg?.name)).trim(),
      url: String(bg?.url || '').trim()
    }))
    .filter(bg => {
      if (!bg.name || !bg.url) return false;
      const key = bg.name.toLowerCase();
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    });
}
function loadReaderBackgroundChoice() {
  try { return String(localStorage.getItem(READER_BACKGROUND_KEY) || '').trim(); } catch { return ''; }
}
function saveReaderBackgroundChoice(value) {
  try { localStorage.setItem(READER_BACKGROUND_KEY, String(value || '')); } catch {}
}
function loadReaderBackgroundBrightness() {
  try {
    const raw = localStorage.getItem(READER_BACKGROUND_BRIGHTNESS_KEY);
    return clampNumber(raw === null ? 72 : raw, 15, 100, 72);
  } catch {
    return 72;
  }
}
function saveReaderBackgroundBrightness(value) {
  try { localStorage.setItem(READER_BACKGROUND_BRIGHTNESS_KEY, String(Math.round(clampNumber(value, 15, 100, 72)))); } catch {}
}
function setReaderBackgroundBrightness(value) {
  state.reader.backgroundBrightness = clampNumber(value, 15, 100, 72);
  saveReaderBackgroundBrightness(state.reader.backgroundBrightness);
  applyReaderBackground();
  updateReaderOverlay();
}
function selectedReaderBackground() {
  const selected = String(state.reader.background || '').trim();
  return (state.reader.backgrounds || []).find(bg => bg && bg.name === selected) || null;
}
function applyReaderBackground() {
  const stage = $('readerStage');
  const preview = $('readerBackgroundPreview');
  const label = $('readerBackgroundName');
  const select = $('readerBackgroundSelect');
  const brightnessSlider = $('readerBackgroundBrightness');
  const brightnessValue = $('readerBackgroundBrightnessValue');
  const bg = selectedReaderBackground();
  const brightness = state.reader.backgroundBrightness = clampNumber(state.reader.backgroundBrightness ?? loadReaderBackgroundBrightness(), 15, 100, 72);
  const cssBrightness = Math.max(.15, Math.min(1, brightness / 100));
  const dimOpacity = Math.max(0, Math.min(.82, (100 - brightness) / 100 * .76));

  if (stage) {
    stage.style.setProperty('--reader-background-brightness', cssBrightness.toFixed(3));
    stage.style.setProperty('--reader-background-dim-opacity', dimOpacity.toFixed(3));
    if (bg?.url) {
      stage.classList.add('reader-custom-background');
      stage.style.setProperty('--reader-background-image', `url("${String(bg.url).replace(/"/g, '%22')}")`);
    } else {
      stage.classList.remove('reader-custom-background');
      stage.style.removeProperty('--reader-background-image');
    }
  }

  if (preview) {
    preview.classList.toggle('empty', !bg?.url);
    preview.style.backgroundImage = bg?.url ? `url("${String(bg.url).replace(/"/g, '%22')}")` : '';
    preview.style.filter = `brightness(${cssBrightness.toFixed(3)})`;
  }
  if (label) label.textContent = bg?.displayName || 'Default Gradient';
  if (select && select.value !== (bg?.name || '')) select.value = bg?.name || '';
  if (brightnessSlider && brightnessSlider.value !== String(Math.round(brightness))) brightnessSlider.value = String(Math.round(brightness));
  if (brightnessValue) brightnessValue.textContent = `${Math.round(brightness)}%`;
}
function populateReaderBackgroundSelect() {
  const select = $('readerBackgroundSelect');
  if (!select) return;
  const current = String(state.reader.background || '');
  const options = ['<option value="">Default Gradient</option>'].concat((state.reader.backgrounds || []).map(bg => `<option value="${escapeHtml(bg.name)}">${escapeHtml(bg.displayName || readerBackgroundDisplayName(bg.name))}</option>`));
  select.innerHTML = options.join('');
  select.value = current;
}
async function loadReaderBackgrounds() {
  state.reader.backgroundBrightness = loadReaderBackgroundBrightness();
  const fallback = fallbackReaderBackgrounds();
  try {
    const res = await fetch('/api/reader/backgrounds', { cache: 'no-store' });
    if (!res.ok) throw new Error('Unable to list reader backgrounds');
    const data = await res.json();
    const backgrounds = Array.isArray(data?.backgrounds) ? data.backgrounds : [];
    state.reader.backgrounds = mergeReaderBackgroundLists(backgrounds, fallback);

    const saved = loadReaderBackgroundChoice();
    const savedExists = saved && state.reader.backgrounds.some(bg => bg.name === saved);
    const defaultName = String(data?.defaultName || 'librarydesk.png').trim();
    const defaultExists = defaultName && state.reader.backgrounds.some(bg => bg.name === defaultName);
    state.reader.background = savedExists ? saved : (defaultExists ? defaultName : (state.reader.backgrounds[0]?.name || ''));
    saveReaderBackgroundChoice(state.reader.background);
    populateReaderBackgroundSelect();
    syncReadingProfileBackgroundOptions();
    applyReaderBackground();
  } catch (err) {
    console.warn('Guidevault reader backgrounds unavailable from API; using static /assets/backgrounds fallback.', err);
    state.reader.backgrounds = fallback;
    const saved = loadReaderBackgroundChoice();
    const savedExists = saved && state.reader.backgrounds.some(bg => bg.name === saved);
    state.reader.background = savedExists ? saved : (state.reader.backgrounds.find(bg => bg.name === 'librarydesk.png')?.name || state.reader.backgrounds[0]?.name || '');
    saveReaderBackgroundChoice(state.reader.background);
    populateReaderBackgroundSelect();
    syncReadingProfileBackgroundOptions();
    applyReaderBackground();
  }
}

function setReaderBackground(name) {
  const selected = String(name || '').trim();
  const exists = !selected || (state.reader.backgrounds || []).some(bg => bg.name === selected);
  state.reader.background = exists ? selected : '';
  saveReaderBackgroundChoice(state.reader.background);
  applyReaderBackground();
  updateReaderOverlay();
}

function rgbParts(rgb, fallback = [226, 224, 216]) {
  const source = Array.isArray(rgb) && rgb.length >= 3 ? rgb : fallback;
  return source.slice(0, 3).map(v => Math.max(0, Math.min(255, Math.round(Number(v) || 0))));
}
function rgbString(rgb, fallback = [226, 224, 216]) {
  return rgbParts(rgb, fallback).join(', ');
}
function mixRgb(a, b, t) {
  const left = rgbParts(a);
  const right = rgbParts(b);
  const p = Math.max(0, Math.min(1, Number(t) || 0));
  return left.map((v, i) => Math.round(v + (right[i] - v) * p));
}
function readerSampleDominantPageColor(imgEl, fallback = [226, 224, 216]) {
  if (!imgEl || !imgEl.complete || !(imgEl.naturalWidth > 0) || !(imgEl.naturalHeight > 0)) return rgbParts(fallback);
  try {
    const sampleSize = 36;
    const canvas = document.createElement('canvas');
    canvas.width = sampleSize;
    canvas.height = sampleSize;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return rgbParts(fallback);
    ctx.drawImage(imgEl, 0, 0, sampleSize, sampleSize);
    const data = ctx.getImageData(0, 0, sampleSize, sampleSize).data;
    const buckets = new Map();
    for (let i = 0; i < data.length; i += 4) {
      const a = data[i + 3] / 255;
      if (a < .2) continue;
      const r = data[i], g = data[i + 1], b = data[i + 2];
      const max = Math.max(r, g, b);
      const min = Math.min(r, g, b);
      const saturation = max <= 0 ? 0 : (max - min) / max;
      const value = max / 255;
      // Page stacks should mostly read as paper. Keep bright/off-white scanned paper
      // as a valid dominant color, but de-emphasize pure black borders and tiny saturated art pixels.
      let weight = 1;
      if (value < .10) weight *= .08;
      if (saturation > .55 && value < .88) weight *= .38;
      if (value > .88 && saturation < .16) weight *= 2.8;
      if (value > .72 && saturation < .30) weight *= 1.8;
      const qr = Math.round(r / 24) * 24;
      const qg = Math.round(g / 24) * 24;
      const qb = Math.round(b / 24) * 24;
      const key = `${qr},${qg},${qb}`;
      const existing = buckets.get(key) || { weight: 0, r: 0, g: 0, b: 0 };
      existing.weight += weight;
      existing.r += r * weight;
      existing.g += g * weight;
      existing.b += b * weight;
      buckets.set(key, existing);
    }
    let best = null;
    for (const bucket of buckets.values()) {
      if (!best || bucket.weight > best.weight) best = bucket;
    }
    if (!best || best.weight <= 0) return rgbParts(fallback);
    const color = [best.r / best.weight, best.g / best.weight, best.b / best.weight].map(Math.round);
    // Pull the sampled color slightly toward warm paper so saturated/printed pages do not
    // make the page block look like transparent artwork or background bleed.
    const brightness = Math.max(...color) / 255;
    const paperMix = brightness > .70 ? .22 : .42;
    return mixRgb(color, [232, 229, 218], paperMix);
  } catch {
    return rgbParts(fallback);
  }
}
function setReaderStackColorVars(prefix, rgb) {
  const book = $('book');
  if (!book) return;
  const base = rgbParts(rgb);
  const light = mixRgb(base, [255, 255, 250], .42);
  const mid = mixRgb(base, [224, 220, 208], .18);
  const dark = mixRgb(base, [72, 70, 66], .34);
  book.style.setProperty(`--gv-${prefix}-stack-rgb`, rgbString(base));
  book.style.setProperty(`--gv-${prefix}-stack-light-rgb`, rgbString(light));
  book.style.setProperty(`--gv-${prefix}-stack-mid-rgb`, rgbString(mid));
  book.style.setProperty(`--gv-${prefix}-stack-dark-rgb`, rgbString(dark));
}
function updateReaderPageStackColors() {
  const book = $('book');
  if (!book) return;
  const leftImg = $('pageLeftImage');
  const rightImg = $('pageRightImage');
  const fallback = [226, 224, 216];
  const rightColor = readerSampleDominantPageColor(rightImg, fallback);
  const leftColor = readerSampleDominantPageColor(leftImg?.src ? leftImg : rightImg, rightColor);
  const bindingColor = mixRgb(leftColor, rightColor, .5);
  setReaderStackColorVars('left', leftColor);
  setReaderStackColorVars('right', rightColor);
  book.style.setProperty('--gv-binding-paper-rgb', rgbString(bindingColor));
}

function applyReaderShadingSettings() {
  const book = $('book');
  const cfg = state.reader.shading = normalizeReaderShading(state.reader.shading || loadReaderShading());
  if (!book) return;

  const binding = cfg.bindingEnabled ? cfg.bindingDepth / 100 : 0;
  const outer = cfg.outerEnabled ? cfg.outerDepth / 100 : 0;

  book.classList.toggle('reader-binding-shade-off', !cfg.bindingEnabled || cfg.bindingDepth <= 0);
  book.classList.toggle('reader-outer-shade-off', !cfg.outerEnabled || cfg.outerDepth <= 0);

  setCssVar(book, '--gv-binding-width', `${Math.round(34 + binding * 92)}px`);
  setCssVar(book, '--gv-binding-blur', `${(1.4 + binding * 4.6).toFixed(1)}px`);
  setCssVar(book, '--gv-binding-opacity', alpha(0.16 + binding * 0.84));
  setCssVar(book, '--gv-binding-radial-1', alpha(0.08 + binding * 0.34));
  setCssVar(book, '--gv-binding-radial-2', alpha(0.05 + binding * 0.25));
  setCssVar(book, '--gv-binding-radial-3', alpha(0.02 + binding * 0.13));
  setCssVar(book, '--gv-binding-line-soft', alpha(0.025 + binding * 0.095));
  setCssVar(book, '--gv-binding-line-mid', alpha(0.075 + binding * 0.37));
  setCssVar(book, '--gv-binding-highlight', alpha(binding * 0.07));
  setCssVar(book, '--gv-binding-centerline', alpha(0.07 + binding * 0.26));
  setCssVar(book, '--gv-binding-centerline-highlight', alpha(binding * 0.12));

  // Push the page-edge shading harder so the slider range is more visible.
  // This affects the far-left edge of the left page image and the far-right edge of the right page image.
  setCssVar(book, '--gv-outer-width', `${Math.round(24 + outer * 84)}px`);
  setCssVar(book, '--gv-outer-blur', `${(1.2 + outer * 4.0).toFixed(1)}px`);
  setCssVar(book, '--gv-outer-opacity', alpha(0.30 + outer * 0.70));
  setCssVar(book, '--gv-outer-alpha-1', alpha(0.14 + outer * 0.54));
  setCssVar(book, '--gv-outer-alpha-2', alpha(0.08 + outer * 0.32));
  setCssVar(book, '--gv-outer-alpha-3', alpha(0.028 + outer * 0.16));
}

function pageImageContainBounds(pageEl, imgEl, side) {
  if (!pageEl || !imgEl) return null;
  // Use layout dimensions instead of getBoundingClientRect() so reader zoom does
  // not distort the page/shadow geometry used by the experimental turn overlays.
  const w = pageEl.clientWidth || pageEl.offsetWidth || 0;
  const h = pageEl.clientHeight || pageEl.offsetHeight || 0;
  if (!w || !h) return null;

  const naturalW = imgEl.naturalWidth || 0;
  const naturalH = imgEl.naturalHeight || 0;
  if (!naturalW || !naturalH) {
    return { left: 0, right: w, top: 0, height: h };
  }

  const ratio = naturalW / naturalH;
  let imageW = w;
  let imageH = imageW / ratio;
  if (imageH > h) {
    imageH = h;
    imageW = imageH * ratio;
  }

  const y = Math.max(0, (h - imageH) / 2);
  const extraX = Math.max(0, w - imageW);
  const book = $('book');
  const centerSingleVisual = !!book && (
    book.classList.contains('cover-mode')
    || book.classList.contains('single-page-mode')
    || (book.classList.contains('adaptive-spread-mode') && side === 'right')
    || (book.classList.contains('reader-single-visual') && side === 'right')
  );
  // Two-page spreads anchor each page toward the binding. Single/cover pages use
  // object-position:center, so their measured bounds must be centered too. This
  // keeps portrait covers from placing the remaining-page stack on the left edge.
  const x = centerSingleVisual ? extraX / 2 : (side === 'left' ? extraX : 0);
  return { left: x, right: x + imageW, top: y, height: imageH };
}

function setPageEdgeBounds(pageId, imageId, side) {
  const page = $(pageId);
  const image = $(imageId);
  if (!page || !image) return;
  const bounds = pageImageContainBounds(page, image, side);
  if (!bounds) return;
  page.style.setProperty('--gv-page-image-left', `${Math.round(bounds.left)}px`);
  page.style.setProperty('--gv-page-image-right', `${Math.round(bounds.right)}px`);
  page.style.setProperty('--gv-page-image-top', `${Math.round(bounds.top)}px`);
  page.style.setProperty('--gv-page-image-height', `${Math.round(bounds.height)}px`);
}

function updateReaderMeasuredVisualBounds() {
  const book = $('book');
  const shell = $('pageShell');
  if (!book || !shell) return;

  const leftPage = $('pageLeft');
  const leftImg = $('pageLeftImage');
  const rightPage = $('pageRight');
  const rightImg = $('pageRightImage');
  const shellLeft = shell.offsetLeft || 0;
  const shellTop = shell.offsetTop || 0;
  const rightBounds = pageImageContainBounds(rightPage, rightImg, 'right');
  const leftBounds = pageImageContainBounds(leftPage, leftImg, 'left');

  const setVars = (vars) => {
    Object.entries(vars).forEach(([name, value]) => {
      const px = typeof value === 'number' ? `${Math.round(value)}px` : value;
      book.style.setProperty(name, px);
      shell.style.setProperty(name, px);
    });
  };

  if (book.classList.contains('adaptive-spread-mode')) {
    if (!rightPage || !rightBounds) {
      const w = book.clientWidth || shell.clientWidth || 1;
      const h = book.clientHeight || shell.clientHeight || 1;
      setVars({
        '--gv-visual-left': 0,
        '--gv-visual-right': w,
        '--gv-visual-top': 0,
        '--gv-visual-height': h,
        '--gv-binding-left': w / 2,
        '--gv-binding-top': 0,
        '--gv-binding-height': h
      });
      return;
    }
    const top = shellTop + (rightPage.offsetTop || 0) + rightBounds.top;
    const height = rightBounds.height;
    const left = shellLeft + (rightPage.offsetLeft || 0) + rightBounds.left;
    const right = shellLeft + (rightPage.offsetLeft || 0) + rightBounds.right;
    setVars({
      '--gv-visual-left': left,
      '--gv-visual-right': right,
      '--gv-visual-top': top,
      '--gv-visual-height': height,
      '--gv-binding-left': (left + right) / 2,
      '--gv-binding-top': top,
      '--gv-binding-height': height
    });
    return;
  }

  if (book.classList.contains('cover-mode') || book.classList.contains('single-page-mode') || book.classList.contains('reader-single-visual') || !leftBounds || !leftPage) {
    if (!rightPage || !rightBounds) {
      const w = book.clientWidth || shell.clientWidth || 1;
      const h = book.clientHeight || shell.clientHeight || 1;
      setVars({
        '--gv-visual-left': 0,
        '--gv-visual-right': w,
        '--gv-visual-top': 0,
        '--gv-visual-height': h,
        '--gv-binding-left': w / 2,
        '--gv-binding-top': 0,
        '--gv-binding-height': h
      });
      return;
    }
    const top = shellTop + (rightPage.offsetTop || 0) + rightBounds.top;
    const height = rightBounds.height;
    const left = shellLeft + (rightPage.offsetLeft || 0) + rightBounds.left;
    const right = shellLeft + (rightPage.offsetLeft || 0) + rightBounds.right;
    setVars({
      '--gv-visual-left': left,
      '--gv-visual-right': right,
      '--gv-visual-top': top,
      '--gv-visual-height': height,
      '--gv-binding-left': left,
      '--gv-binding-top': top,
      '--gv-binding-height': height
    });
    return;
  }

  if (!rightPage || !rightBounds) return;
  const leftTop = shellTop + (leftPage.offsetTop || 0) + leftBounds.top;
  const rightTop = shellTop + (rightPage.offsetTop || 0) + rightBounds.top;
  const top = Math.min(leftTop, rightTop);
  const bottom = Math.max(leftTop + leftBounds.height, rightTop + rightBounds.height);
  const leftEdge = shellLeft + (leftPage.offsetLeft || 0) + leftBounds.left;
  const leftInnerEdge = shellLeft + (leftPage.offsetLeft || 0) + leftBounds.right;
  const rightInnerEdge = shellLeft + (rightPage.offsetLeft || 0) + rightBounds.left;
  const rightEdge = shellLeft + (rightPage.offsetLeft || 0) + rightBounds.right;
  const bindingLeft = (leftInnerEdge + rightInnerEdge) / 2;
  setVars({
    '--gv-visual-left': leftEdge,
    '--gv-visual-right': rightEdge,
    '--gv-visual-top': top,
    '--gv-visual-height': Math.max(1, bottom - top),
    '--gv-binding-left': bindingLeft,
    '--gv-binding-top': top,
    '--gv-binding-height': Math.max(1, bottom - top)
  });
}


function updateReaderVisualModeFlags(spread = null) {
  const book = $('book');
  if (!book) return false;
  const current = spread || spreadForIndex(state.reader.index);
  const leftPage = $('pageLeft');
  const leftImg = $('pageLeftImage');
  const leftHidden = !leftPage || leftPage.classList.contains('hidden') || !leftImg || !leftImg.src;
  const visualSingle = !current?.isAdaptiveSpread && (!!current?.isSingle
    || book.classList.contains('cover-mode')
    || book.classList.contains('single-page-mode')
    || leftHidden);
  book.classList.toggle('reader-single-visual', visualSingle);
  book.classList.toggle('reader-cover-visual', !!current?.isCover || book.classList.contains('cover-mode'));
  return visualSingle;
}

function updateReaderPageEdgeShadingBounds() {
  const book = $('book');
  if (!book) return;
  const visualSingle = updateReaderVisualModeFlags();
  if (book.classList.contains('adaptive-spread-mode')) {
    setPageEdgeBounds('pageRight', 'pageRightImage', 'right');
  } else if (!visualSingle && !book.classList.contains('cover-mode') && !book.classList.contains('single-page-mode')) {
    setPageEdgeBounds('pageLeft', 'pageLeftImage', 'left');
    setPageEdgeBounds('pageRight', 'pageRightImage', 'right');
  } else {
    setPageEdgeBounds('pageRight', 'pageRightImage', 'right');
  }
  updateReaderMeasuredVisualBounds();
  updateReaderPageStackGeometry();
  updateReaderPageStackColors();
}

function updateReaderPageStackGeometry() {
  const book = $('book');
  const shell = $('pageShell');
  if (!book || !shell) return;

  const setDefault = () => {
    const bw = book.clientWidth || shell.clientWidth || 0;
    const bh = book.clientHeight || shell.clientHeight || 0;
    book.style.setProperty('--gv-stack-top', '0px');
    book.style.setProperty('--gv-stack-height', `${Math.max(1, Math.round(bh))}px`);
    book.style.setProperty('--gv-left-stack-edge', `${Math.round(bw * .5)}px`);
    book.style.setProperty('--gv-right-stack-edge', `${Math.round(bw * .5)}px`);
  };

  const shellLeft = shell.offsetLeft || 0;
  const shellTop = shell.offsetTop || 0;
  const leftPage = $('pageLeft');
  const leftImg = $('pageLeftImage');
  const rightPage = $('pageRight');
  const rightImg = $('pageRightImage');

  const rightBounds = pageImageContainBounds(rightPage, rightImg, 'right');
  const leftBounds = pageImageContainBounds(leftPage, leftImg, 'left');

  if (book.classList.contains('adaptive-spread-mode')) {
    if (!rightPage || !rightBounds) { setDefault(); return; }
    const top = shellTop + (rightPage.offsetTop || 0) + rightBounds.top;
    const height = rightBounds.height;
    const leftEdge = shellLeft + (rightPage.offsetLeft || 0) + rightBounds.left;
    const rightEdge = shellLeft + (rightPage.offsetLeft || 0) + rightBounds.right;
    book.style.setProperty('--gv-stack-top', `${Math.round(top)}px`);
    book.style.setProperty('--gv-stack-height', `${Math.max(1, Math.round(height))}px`);
    book.style.setProperty('--gv-left-stack-edge', `${Math.round(leftEdge)}px`);
    book.style.setProperty('--gv-right-stack-edge', `${Math.round(rightEdge)}px`);
    return;
  }

  if (book.classList.contains('cover-mode') || book.classList.contains('single-page-mode') || book.classList.contains('reader-single-visual') || !leftBounds) {
    if (!rightPage || !rightBounds) { setDefault(); return; }
    const top = shellTop + (rightPage.offsetTop || 0) + rightBounds.top;
    const height = rightBounds.height;
    const rightLeft = shellLeft + (rightPage.offsetLeft || 0) + rightBounds.left;
    const rightEdge = shellLeft + (rightPage.offsetLeft || 0) + rightBounds.right;
    book.style.setProperty('--gv-stack-top', `${Math.round(top)}px`);
    book.style.setProperty('--gv-stack-height', `${Math.max(1, Math.round(height))}px`);
    book.style.setProperty('--gv-left-stack-edge', `${Math.round(rightLeft)}px`);
    book.style.setProperty('--gv-right-stack-edge', `${Math.round(rightEdge)}px`);
    return;
  }

  if (!rightPage || !rightBounds) { setDefault(); return; }
  const top = shellTop + (leftPage.offsetTop || 0) + leftBounds.top;
  const height = Math.max(leftBounds.height, rightBounds.height || 0);
  const leftEdge = shellLeft + (leftPage.offsetLeft || 0) + leftBounds.left;
  const rightEdge = shellLeft + (rightPage.offsetLeft || 0) + rightBounds.right;
  book.style.setProperty('--gv-stack-top', `${Math.round(top)}px`);
  book.style.setProperty('--gv-stack-height', `${Math.max(1, Math.round(height))}px`);
  book.style.setProperty('--gv-left-stack-edge', `${Math.round(leftEdge)}px`);
  book.style.setProperty('--gv-right-stack-edge', `${Math.round(rightEdge)}px`);
}

function scheduleReaderPageEdgeShadingBounds() {
  requestAnimationFrame(() => requestAnimationFrame(updateReaderPageEdgeShadingBounds));
}

function updateReaderShadingControls() {
  const cfg = state.reader.shading = normalizeReaderShading(state.reader.shading || loadReaderShading());
  const bindingToggle = $('readerBindingShadeEnabled');
  const bindingSlider = $('readerBindingShadeSlider');
  const outerToggle = $('readerOuterShadeEnabled');
  const outerSlider = $('readerOuterShadeSlider');
  if (bindingToggle) bindingToggle.checked = !!cfg.bindingEnabled;
  if (bindingSlider) { bindingSlider.value = String(cfg.bindingDepth); bindingSlider.disabled = !cfg.bindingEnabled; }
  if (outerToggle) outerToggle.checked = !!cfg.outerEnabled;
  if (outerSlider) { outerSlider.value = String(cfg.outerDepth); outerSlider.disabled = !cfg.outerEnabled; }
}
function updateReaderShadingSetting(key, value) {
  state.reader.shading = normalizeReaderShading(state.reader.shading || loadReaderShading());
  if (key === 'bindingEnabled') state.reader.shading.bindingEnabled = !!value;
  if (key === 'bindingDepth') state.reader.shading.bindingDepth = clampNumber(value, 0, 100, state.reader.shading.bindingDepth);
  if (key === 'outerEnabled') state.reader.shading.outerEnabled = !!value;
  if (key === 'outerDepth') state.reader.shading.outerDepth = clampNumber(value, 0, 100, state.reader.shading.outerDepth);
  state.reader.shading = normalizeReaderShading(state.reader.shading);
  saveReaderShading();
  applyReaderShadingSettings();
  scheduleReaderPageEdgeShadingBounds();
  updateReaderShadingControls();
}


function defaultReaderMagnifier() {
  return { width: 320, height: 220, opacity: 92, zoom: 220, longClickEnabled: true };
}
function normalizeReaderMagnifier(value = {}) {
  const defaults = defaultReaderMagnifier();
  return {
    width: clampNumber(value.width, 160, 520, defaults.width),
    height: clampNumber(value.height, 120, 420, defaults.height),
    opacity: clampNumber(value.opacity, 35, 100, defaults.opacity),
    zoom: clampNumber(value.zoom, 130, 360, defaults.zoom),
    longClickEnabled: value.longClickEnabled !== false
  };
}
function loadReaderMagnifier() {
  try {
    return normalizeReaderMagnifier(JSON.parse(localStorage.getItem(READER_MAGNIFIER_KEY) || '{}'));
  } catch {
    return normalizeReaderMagnifier();
  }
}
function saveReaderMagnifier() {
  try { localStorage.setItem(READER_MAGNIFIER_KEY, JSON.stringify(normalizeReaderMagnifier(state.reader.magnifier || {}))); } catch {}
}
function updateReaderMagnifierControls() {
  const cfg = state.reader.magnifier = normalizeReaderMagnifier(state.reader.magnifier || loadReaderMagnifier());
  const width = $('readerMagnifierWidth');
  const height = $('readerMagnifierHeight');
  const opacity = $('readerMagnifierOpacity');
  const zoom = $('readerMagnifierZoom');
  const longClick = $('readerMagnifierLongClick');
  if (width) width.value = String(cfg.width);
  if (height) height.value = String(cfg.height);
  if (opacity) opacity.value = String(cfg.opacity);
  if (zoom) zoom.value = String(cfg.zoom);
  if (longClick) longClick.checked = !!cfg.longClickEnabled;
  const button = $('readerMagnifierToggle');
  if (button) {
    button.classList.toggle('active', !!state.reader.magnifierActive);
    button.setAttribute('aria-pressed', state.reader.magnifierActive ? 'true' : 'false');
    button.title = state.reader.magnifierActive ? 'Turn magnifier off' : 'Turn magnifier on';
  }
  const menu = $('readerMagnifierMenu');
  if (menu) {
    menu.classList.toggle('active', !!state.reader.magnifierSettingsVisible);
    menu.setAttribute('aria-expanded', state.reader.magnifierSettingsVisible ? 'true' : 'false');
  }
  const panel = $('readerMagnifierPanel');
  if (panel) {
    const open = !!state.reader.magnifierSettingsVisible && !!state.reader.overlayVisible;
    panel.classList.toggle('hidden', !open);
    panel.classList.toggle('open', open);
  }
  applyReaderMagnifierSettings();
}
function updateReaderMagnifierSetting(key, value) {
  state.reader.magnifier = normalizeReaderMagnifier(state.reader.magnifier || loadReaderMagnifier());
  if (key === 'width') state.reader.magnifier.width = clampNumber(value, 160, 520, state.reader.magnifier.width);
  if (key === 'height') state.reader.magnifier.height = clampNumber(value, 120, 420, state.reader.magnifier.height);
  if (key === 'opacity') state.reader.magnifier.opacity = clampNumber(value, 35, 100, state.reader.magnifier.opacity);
  if (key === 'zoom') state.reader.magnifier.zoom = clampNumber(value, 130, 360, state.reader.magnifier.zoom);
  if (key === 'longClickEnabled') state.reader.magnifier.longClickEnabled = !!value;
  state.reader.magnifier = normalizeReaderMagnifier(state.reader.magnifier);
  saveReaderMagnifier();
  updateReaderMagnifierControls();
  if (state.reader.magnifierActive) updateReaderMagnifierFromLastPointer();
}
function applyReaderMagnifierSettings() {
  const lens = $('readerMagnifier');
  const cfg = state.reader.magnifier = normalizeReaderMagnifier(state.reader.magnifier || loadReaderMagnifier());
  if (!lens) return;
  lens.style.setProperty('--reader-magnifier-width', `${Math.round(cfg.width)}px`);
  lens.style.setProperty('--reader-magnifier-height', `${Math.round(cfg.height)}px`);
  lens.style.setProperty('--reader-magnifier-opacity', alpha(cfg.opacity / 100));
}
function setReaderMagnifierPanelVisible(visible) {
  state.reader.magnifierSettingsVisible = !!visible;
  if (state.reader.magnifierSettingsVisible) state.reader.overlayVisible = true;
  updateReaderMagnifierControls();
  if (state.reader.overlayVisible) updateReaderOverlay();
}
function setReaderMagnifierActive(active, event) {
  state.reader.magnifierActive = !!active;
  const lens = $('readerMagnifier');
  if (lens) lens.classList.toggle('hidden', !state.reader.magnifierActive);
  updateReaderMagnifierControls();
  if (state.reader.magnifierActive) {
    updateReaderMagnifierContent();
    updateReaderMagnifierFromPointer(event || state.reader.lastPointerEvent || null);
  }
}
function toggleReaderMagnifier(event) {
  setReaderMagnifierActive(!state.reader.magnifierActive, event);
}
function magnifierSpreadMarkup(spread) {
  if (!spread) return '';
  if (spread.isSingle) {
    return `<div class="magnifier-spread single"><div class="magnifier-page right"><img src="${escapeHtml(spread.rightUrl || '')}" alt="" /></div></div>`;
  }
  return `<div class="magnifier-spread double"><div class="magnifier-page left"><img src="${escapeHtml(spread.leftUrl || '')}" alt="" /></div><div class="magnifier-page right${spread.isBlankRight ? ' blank' : ''}"><img src="${escapeHtml(spread.rightUrl || '')}" alt="" /></div></div>`;
}
function updateReaderMagnifierContent() {
  const content = $('readerMagnifierContent');
  const shell = $('pageShell');
  if (!content || !shell) return;
  const spread = spreadForIndex(state.reader.index);
  content.innerHTML = magnifierSpreadMarkup(spread);
  const shellRect = shell.getBoundingClientRect();
  content.style.width = `${Math.round(shellRect.width)}px`;
  content.style.height = `${Math.round(shellRect.height)}px`;
}
function updateReaderMagnifierFromLastPointer() {
  updateReaderMagnifierFromPointer(state.reader.lastPointerEvent || null);
}
function updateReaderMagnifierFromPointer(event) {
  const lens = $('readerMagnifier');
  const content = $('readerMagnifierContent');
  const stage = $('readerStage');
  const shell = $('pageShell');
  if (!lens || !content || !stage || !shell || !state.reader.magnifierActive) return;
  const cfg = state.reader.magnifier = normalizeReaderMagnifier(state.reader.magnifier || loadReaderMagnifier());
  const stageRect = stage.getBoundingClientRect();
  const shellRect = shell.getBoundingClientRect();
  const pointer = event ? { x: event.clientX, y: event.clientY } : (state.reader.lastPointerPoint || null);
  const fallback = { x: shellRect.left + shellRect.width / 2, y: shellRect.top + shellRect.height / 2 };
  const x = pointer?.x ?? fallback.x;
  const y = pointer?.y ?? fallback.y;
  state.reader.lastPointerPoint = { x, y };

  const width = cfg.width;
  const height = cfg.height;
  const margin = 8;
  const rawLeft = x - stageRect.left - width / 2;
  const rawTop = y - stageRect.top - height / 2;
  const maxLeft = Math.max(margin, stageRect.width - width - margin);
  const maxTop = Math.max(margin, stageRect.height - height - margin);
  const lensLeft = Math.max(margin, Math.min(rawLeft, maxLeft));
  const lensTop = Math.max(margin, Math.min(rawTop, maxTop));
  lens.style.left = `${Math.round(lensLeft)}px`;
  lens.style.top = `${Math.round(lensTop)}px`;

  const localX = Math.max(0, Math.min(x - shellRect.left, shellRect.width));
  const localY = Math.max(0, Math.min(y - shellRect.top, shellRect.height));
  const zoom = cfg.zoom / 100;
  content.style.width = `${Math.round(shellRect.width)}px`;
  content.style.height = `${Math.round(shellRect.height)}px`;
  content.style.transform = `translate3d(${(width / 2 - localX * zoom).toFixed(1)}px, ${(height / 2 - localY * zoom).toFixed(1)}px, 0) scale(${zoom.toFixed(3)})`;
}
function handleReaderPointerMove(event) {
  state.reader.lastPointerEvent = event;
  if (state.reader.magnifierActive) updateReaderMagnifierFromPointer(event);
}
function consumeReaderLongPressClick() {
  if (Date.now() < (state.reader.suppressHitClickUntil || 0)) {
    state.reader.suppressHitClickUntil = 0;
    return true;
  }
  return false;
}
function beginReaderLongPress(event) {
  state.reader.lastPointerEvent = event;
  // Long-press magnifier should only arm while the reader is in clean reading mode.
  // When the bottom tray/settings UI is open, the same press/hold gestures are used
  // for controls and should never pop the magnifier over the panel.
  if (state.reader.overlayVisible || state.reader.advancedVisible || state.reader.magnifierSettingsVisible) {
    cancelReaderLongPress();
    return;
  }
  const cfg = state.reader.magnifier = normalizeReaderMagnifier(state.reader.magnifier || loadReaderMagnifier());
  if (!cfg.longClickEnabled) return;
  window.clearTimeout(state.reader.longPressTimer);
  state.reader.longPressTimer = window.setTimeout(() => {
    if (state.reader.overlayVisible || state.reader.advancedVisible || state.reader.magnifierSettingsVisible) return;
    state.reader.suppressHitClickUntil = Date.now() + 850;
    setReaderOverlayVisible(true);
    toggleReaderMagnifier(event);
  }, 620);
}
function cancelReaderLongPress() {
  window.clearTimeout(state.reader.longPressTimer);
  state.reader.longPressTimer = null;
}

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;' }[ch]));
}
function normalizeName(value) {
  return String(value || '')
    .toLowerCase()
    .replace(/&/g, ' and ')
    .replace(/\(([^)]*)\)/g, ' $1 ')
    .replace(/[^a-z0-9]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function isUnsortedCategoryName(name) {
  const n = normalizeName(name);
  return !n || n === 'unsorted' || n.startsWith('unsorted ') || n.includes(' unsorted ');
}
function isMultiPlatformStrategyCategory(kind, name) {
  const n = normalizeName(name);
  return kind === 'Strategy Guide' && (n === 'multi platform' || n === 'multi platform strategy guides' || (n.includes('multi platform') && n.includes('strategy')));
}
function categorySortRank(kind, name) {
  if (isUnsortedCategoryName(name)) return 0;
  if (isMultiPlatformStrategyCategory(kind, name)) return 1;
  return 2;
}
function compareCategoryNames(kind, a, b) {
  const rank = categorySortRank(kind, a) - categorySortRank(kind, b);
  if (rank) return rank;
  return String(a || '').localeCompare(String(b || ''), undefined, { numeric: true, sensitivity: 'base' });
}
function sortCategoriesForKind(kind, categories) {
  return [...categories].sort((a, b) => compareCategoryNames(kind, a, b));
}
function compareItemsByPinnedCategory(a, b) {
  const kindA = a?.kind || '';
  const kindB = b?.kind || '';
  const rank = categorySortRank(kindA, categoryOf(a)) - categorySortRank(kindB, categoryOf(b));
  if (rank) return rank;
  if (kindA === kindB) return compareCategoryNames(kindA, categoryOf(a), categoryOf(b));
  return 0;
}
function syncRightToggleLabels() {
  const collapsed = document.body.classList.contains('right-collapsed');
  if ($('rightToggleTop')) {
    $('rightToggleTop').textContent = collapsed ? '›' : '‹';
    $('rightToggleTop').title = collapsed ? 'Show details panel' : 'Hide details panel';
    $('rightToggleTop').setAttribute('aria-label', $('rightToggleTop').title);
  }
  if ($('rightToggle')) {
    $('rightToggle').textContent = collapsed ? '‹' : '›';
    $('rightToggle').title = collapsed ? 'Show details' : 'Collapse details';
  }
}
function toggleRightPanel(forceOpen = null) {
  runPanelTransition(() => {
    if (forceOpen === true) document.body.classList.remove('right-collapsed');
    else if (forceOpen === false) document.body.classList.add('right-collapsed');
    else document.body.classList.toggle('right-collapsed');
    syncRightToggleLabels();
  });
}
function activateTab(tab) {
  state.activeTab = tab || 'overview';
  document.querySelectorAll('.tab').forEach(b => b.classList.toggle('active', b.dataset.tab === state.activeTab));
  document.querySelectorAll('.tab-panel').forEach(p => p.classList.toggle('hidden', p.id !== `${state.activeTab}Panel`));
}

function setupHomebarIconFallbacks() {
  const applyFallback = img => {
    const fallback = img.getAttribute('data-fallback-src');
    if (fallback && img.dataset.fallbackUsed !== '1') {
      img.dataset.fallbackUsed = '1';
      img.removeAttribute('data-fallback-src');
      img.src = fallback;
      return;
    }
    img.classList.add('homebar-icon-missing');
  };
  document.querySelectorAll('.homebar-icon img').forEach(img => {
    if (img.dataset.fallbackBound !== '1') {
      img.dataset.fallbackBound = '1';
      img.addEventListener('error', () => applyFallback(img));
    }
    if (img.complete && img.naturalWidth === 0) applyFallback(img);
  });
}

function updateNavActive() {
  document.querySelectorAll('.nav').forEach(btn => {
    const isCustom = !!btn.dataset.customNavId;
    const matchesCustom = isCustom && state.customFilter?.id === btn.dataset.customNavId;
    const matchesCategoryKind = !state.customFilter && !!state.categoryFilter && btn.dataset.filter === state.filter;
    const matchesView = !state.customFilter && !state.categoryFilter && btn.dataset.view === state.viewMode;
    btn.classList.toggle('active', matchesCustom || matchesCategoryKind || matchesView);
  });
  renderCustomSideNavItems();
}
function setupRightPanelResize() {
  const handle = $('rightResizeHandle');
  const panel = $('detailsPanel');
  if (!handle || !panel) return;
  let dragging = false;
  handle.addEventListener('pointerdown', e => { dragging = true; handle.setPointerCapture(e.pointerId); document.body.classList.add('resizing-details'); });
  handle.addEventListener('pointermove', e => {
    if (!dragging || document.body.classList.contains('right-collapsed')) return;
    const width = Math.max(300, Math.min(620, window.innerWidth - e.clientX));
    document.documentElement.style.setProperty('--right', `${width}px`);
  });
  const stop = e => { if (!dragging) return; dragging = false; document.body.classList.remove('resizing-details'); try { handle.releasePointerCapture(e.pointerId); } catch {} };
  handle.addEventListener('pointerup', stop);
  handle.addEventListener('pointercancel', stop);
}

function runPanelTransition(work) {
  const body = document.body;
  body.classList.add('panel-animating');
  try { work(); } finally {
    window.clearTimeout(runPanelTransition._timer);
    runPanelTransition._timer = window.setTimeout(() => body.classList.remove('panel-animating'), 240);
  }
}

function iconKey(value) {
  return String(value || '').toLowerCase().replace(/\([^)]*\)/g, '').replace(/[^a-z0-9]+/g, '');
}
function iconKeyKeepParen(value) {
  return String(value || '').toLowerCase().replace(/[^a-z0-9]+/g, '');
}
function platformIconUrl(name) {
  if (!name || !state.iconMap) return '';
  const raw = String(name || '').trim();
  const candidates = [
    iconKeyKeepParen(raw),
    iconKey(raw),
    iconKey(raw.replace(/\b(super nintendo)\b/i, 'Super Nintendo Entertainment System')),
    iconKey(raw.replace(/\bplaystation\s*\(?ps1\)?\b/i, 'Sony Playstation')),
    iconKey(raw.replace(/\bnes\b/i, 'Nintendo Entertainment System')),
    iconKey(raw.replace(/\bsnes\b/i, 'Super Nintendo Entertainment System')),
    iconKey(raw.replace(/\bn64\b/i, 'Nintendo 64'))
  ].filter(Boolean);
  for (const key of candidates) if (state.iconMap[key]) return state.iconMap[key];
  return '';
}
function platformIconHtml(name, sizeClass = 'platform-icon') {
  const url = platformIconUrl(name);
  return url ? `<img class="${sizeClass}" src="${url}" alt="" loading="lazy" />` : '';
}
function normalizePlatformIconManifestUrl(url) {
  const raw = String(url || '').trim();
  if (!raw) return '';
  if (raw.startsWith('/assets/icons/')) return raw;
  if (raw.startsWith('/icons/')) return `/assets${raw}`;
  return raw;
}

function normalizePlatformIconManifest(map) {
  const normalized = {};
  Object.entries(map || {}).forEach(([key, value]) => {
    normalized[key] = normalizePlatformIconManifestUrl(value);
  });
  return normalized;
}

async function loadPlatformIcons() {
  try {
    let res = await fetch('/assets/icons/platforms/manifest.json', { cache: 'no-store' });
    state.iconMap = res.ok ? normalizePlatformIconManifest(await res.json()) : {};
  } catch {
    state.iconMap = {};
  }
}

async function loadSettings() {
  const res = await fetch('/api/settings/library', { cache: 'no-store' });
  if (!res.ok) throw new Error('Unable to load library settings.');
  const data = await res.json();
  state.libraryPath = data.libraryPath || '';
  state.libraries = Array.isArray(data.libraries) ? data.libraries.map(normalizeLibraryForClient) : [];
  if (!state.libraries.length && state.libraryPath) state.libraries = [{ name: 'Manuals', type: 'Mixed', folders: [state.libraryPath], lastScanned: null }];
  if ($('libraryRootText')) $('libraryRootText').textContent = state.libraryPath || 'Not set';
  if ($('libraryPathInput')) $('libraryPathInput').value = state.libraryPath || '';
  renderLibrariesSettings();
}

async function loadLibrary() {
  try {
    const iconPromise = Object.keys(state.iconMap || {}).length ? Promise.resolve() : loadPlatformIcons();
    const settingsPromise = loadSettings().catch(err => { console.warn('Settings load failed', err); });
    const libraryPromise = fetch(`/api/library?_=${Date.now()}`, { cache: 'no-store' });
    await Promise.all([iconPromise, settingsPromise]);
    const res = await libraryPromise;
    if (!res.ok) throw new Error(await res.text());
    const data = await res.json();
    state.items = Array.isArray(data) ? data : (data.items || []);
    applyClientMetadataOverridesToLibrary();
    state.libraryLoadedOnce = true;
    if (state.selected) state.selected = state.items.find(i => i.id === state.selected.id) || null;
    applyFilters();
    if (!$('settingsReadingProfilesPanel')?.classList.contains('hidden')) renderReadingProfileSettings();
  } catch (err) {
    console.error(err);
    const hadItems = Array.isArray(state.items) && state.items.length > 0;
    if (!hadItems) {
      state.items = [];
      state.filtered = [];
      render();
    }
    setStatus('Library failed to load. Check the terminal for scan errors; existing results were kept if available.');
    console.warn('Library failed to load. Check the terminal for scan errors.');
  }
}

function setStatus(message = '') {
  const text = String(message || '');
  if ($('libraryDialogStatus')) $('libraryDialogStatus').textContent = text;
  if ($('settingsLibraryStatus')) $('settingsLibraryStatus').textContent = text;
}


function showAppConfirm(options = {}) {
  const modal = $('appConfirmModal');
  const title = $('appConfirmTitle');
  const message = $('appConfirmMessage');
  const ok = $('appConfirmOk');
  const cancel = $('appConfirmCancel');
  if (!modal || !ok || !cancel) {
    console.warn('App confirmation modal is missing from the page.');
    return Promise.resolve(false);
  }

  if (title) title.textContent = options.title || 'Confirm action';
  if (message) message.textContent = options.message || 'Are you sure?';
  ok.textContent = options.okText || 'OK';
  ok.classList.toggle('danger', !!options.danger);
  ok.classList.toggle('primary', !options.danger);
  cancel.textContent = options.cancelText || 'Cancel';

  modal.classList.remove('hidden');
  modal.setAttribute('aria-hidden', 'false');
  window.setTimeout(() => ok.focus(), 0);

  return new Promise(resolve => {
    let settled = false;
    const cleanup = value => {
      if (settled) return;
      settled = true;
      modal.classList.add('hidden');
      modal.setAttribute('aria-hidden', 'true');
      ok.removeEventListener('click', onOk);
      cancel.removeEventListener('click', onCancel);
      modal.removeEventListener('click', onBackdrop);
      document.removeEventListener('keydown', onKeyDown, true);
      resolve(value);
    };
    const onOk = e => { e.preventDefault(); cleanup(true); };
    const onCancel = e => { e.preventDefault(); cleanup(false); };
    const onBackdrop = e => { if (e.target === modal) cleanup(false); };
    const onKeyDown = e => {
      if (e.key === 'Escape') { e.preventDefault(); cleanup(false); }
      if (e.key === 'Enter' && !settled && document.activeElement !== cancel) { e.preventDefault(); cleanup(true); }
    };
    ok.addEventListener('click', onOk);
    cancel.addEventListener('click', onCancel);
    modal.addEventListener('click', onBackdrop);
    document.addEventListener('keydown', onKeyDown, true);
  });
}

function normalizeLibraryForClient(lib) {
  return {
    name: lib?.name || lib?.Name || 'Library',
    type: lib?.type || lib?.Type || 'Mixed',
    folders: Array.isArray(lib?.folders) ? lib.folders : (Array.isArray(lib?.Folders) ? lib.Folders : (lib?.folder || lib?.Folder ? [lib?.folder || lib?.Folder] : [])),
    lastScanned: lib?.lastScanned || lib?.LastScanned || null
  };
}

function itemBelongsToLibrary(item, library) {
  if (!item || !library) return false;
  const libName = String(library.name || library.Name || '').trim().toLowerCase();
  if (libName && String(item.libraryName || item.LibraryName || '').trim().toLowerCase() === libName) return true;
  const folders = Array.isArray(library.folders) ? library.folders : (Array.isArray(library.Folders) ? library.Folders : []);
  const itemPath = String(item.path || item.Path || '').replace(/\\/g, '/').toLowerCase();
  return folders.some(folder => {
    const root = String(folder || '').replace(/\\/g, '/').toLowerCase().replace(/\/+$/, '');
    return root && itemPath.startsWith(root + '/');
  });
}

function removeItemsForLibrary(library) {
  const before = Array.isArray(state.items) ? state.items.length : 0;
  state.items = (state.items || []).filter(item => !itemBelongsToLibrary(item, library));
  if (state.selected && itemBelongsToLibrary(state.selected, library)) state.selected = null;
  applyFilters();
  return Math.max(0, before - state.items.length);
}

function renderLibrariesSettings() {
  const body = $('librariesTableBody');
  if (!body) return;
  body.innerHTML = (state.libraries || []).map((lib, index) => {
    const folder = (lib.folders || [])[0] || '';
    return `<tr>
      <td><strong>${escapeHtml(lib.name)}</strong></td>
      <td>${escapeHtml(lib.type || 'Mixed')}</td>
      <td><div class="library-folder-path">${folder ? escapeHtml(folder) : '<span class="sub">No folder set</span>'}</div></td>
      <td class="library-actions">
        <button class="small-icon rescan-library" data-index="${index}" title="Rescan this library">⟳</button>
        <button class="small-icon edit-library" data-index="${index}" title="Edit library">✎</button>
        <button class="small-icon danger remove-library" data-index="${index}" title="Remove library">ðŸ—‘</button>
      </td>
    </tr>`;
  }).join('') || `<tr><td colspan="4" class="empty-row">No libraries yet. Add a library folder to begin.</td></tr>`;

  body.querySelectorAll('.edit-library').forEach(btn => btn.addEventListener('click', () => openLibraryEditor(Number(btn.dataset.index))));
  body.querySelectorAll('.remove-library').forEach(btn => btn.addEventListener('click', async () => {
    const index = Number(btn.dataset.index);
    const lib = state.libraries[index];
    if (!lib) return;
    const confirmed = await showAppConfirm({
      title: 'Remove library?',
      message: `Remove library "${lib.name}" from Guidevault? Source files will not be touched, but all indexed items from this library will be removed from Guidevault.`,
      okText: 'Remove Library',
      danger: true
    });
    if (!confirmed) return;
    const localTaskId = upsertLibraryTask({
      title: 'Library removal',
      message: `Removing ${lib.name} and clearing its indexed items...`,
      progress: 3,
      kind: 'library-removal'
    });
    state.libraries.splice(index, 1);
    const removedCount = removeItemsForLibrary(lib);
    renderLibrariesSettings();
    await saveLibraries('Library removed.', {
      localTaskId,
      taskTitle: 'Library removal',
      startMessage: removedCount > 0
        ? `Removed ${removedCount} indexed item(s) locally. Updating library index...`
        : 'Updating library index...',
      completeMessage: 'Library removed. Indexed items cleared.',
      operation: 'remove-library',
      taskKind: 'library-removal'
    });
  }));
  body.querySelectorAll('.rescan-library').forEach(btn => btn.addEventListener('click', async () => {
    // Backend rescans all configured libraries for now. This keeps the UI ready for per-library scan later.
    await rescanLibrary();
  }));
}

function openLibraryEditor(index = null) {
  state.editingLibraryIndex = Number.isInteger(index) ? index : null;
  const lib = state.editingLibraryIndex === null ? { name: '', type: 'Mixed', folders: [] } : state.libraries[state.editingLibraryIndex];
  if (!$('libraryEditor')) return;
  $('libraryEditor').classList.remove('hidden');
  $('libraryEditorTitle').textContent = state.editingLibraryIndex === null ? 'Add Library' : 'Edit Library';
  $('libraryNameInput').value = lib?.name || '';
  $('libraryTypeInput').value = lib?.type || 'Mixed';
  $('libraryFolderInput').value = (lib?.folders || [])[0] || '';
  $('libraryNameInput').focus();
}

function closeLibraryEditor() {
  state.editingLibraryIndex = null;
  if ($('libraryEditor')) $('libraryEditor').classList.add('hidden');
}

async function saveLibraryEditor() {
  const name = $('libraryNameInput')?.value.trim() || 'Library';
  const type = $('libraryTypeInput')?.value || 'Mixed';
  const folder = $('libraryFolderInput')?.value.trim() || '';
  if (!folder) { setStatus('Add one folder path for this library.'); return; }
  const lib = { name, type, folders: [folder], lastScanned: null };
  if (state.editingLibraryIndex === null) state.libraries.push(lib);
  else state.libraries[state.editingLibraryIndex] = lib;
  closeLibraryEditor();
  await saveLibraries('Library saved.');
}

async function saveLibraries(successMessage = 'Libraries saved.', options = {}) {
  const taskTitle = options.taskTitle || 'Library scan';
  const taskKind = options.taskKind || (String(taskTitle).toLowerCase().includes('removal') ? 'library-removal' : 'library-scan');
  const localTaskId = options.localTaskId || upsertLibraryTask({
    title: taskTitle,
    message: options.startMessage || 'Saving library settings...',
    progress: 2,
    kind: taskKind
  });
  try {
    updateLibraryTask(localTaskId, options.startMessage || 'Saving library settings...', 8, 'running', taskTitle);
    const cleaned = (state.libraries || []).map(lib => {
      const folder = String((lib?.folders || [])[0] || lib?.folder || '').trim();
      return {
        name: lib?.name || 'Library',
        type: lib?.type || 'Mixed',
        folder,
        folders: folder ? [folder] : [],
        lastScanned: lib?.lastScanned || null
      };
    }).filter(lib => lib.folder);
    const payload = { libraries: cleaned, operation: options.operation || '' };
    const res = await fetch('/api/settings/libraries', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(payload) });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) {
      const msg = data?.error || `Unable to save libraries. HTTP ${res.status}`;
      updateLibraryTask(localTaskId, msg, 100, 'failed', taskTitle);
      setStatus(msg);
      alert(msg);
      return;
    }
    state.libraries = Array.isArray(data?.libraries) ? data.libraries.map(normalizeLibraryForClient) : cleaned.map(normalizeLibraryForClient);
    renderLibrariesSettings();
    const taskId = data?.taskId || data?.TaskId || '';
    if (taskId) {
      replaceLibraryTask(localTaskId, {
        id: taskId,
        kind: taskKind,
        title: taskTitle,
        status: 'running',
        message: data?.message || options.startMessage || (taskKind === 'library-removal' ? 'Library removal started.' : 'Library scan started.'),
        progressPercent: 10,
        updatedAt: new Date().toISOString()
      });
    } else {
      updateLibraryTask(localTaskId, options.completeMessage || successMessage, 100, 'completed', taskTitle);
    }
    await pollTasks(true);
    installGlobalDetailDelegate();
  } catch (err) {
    const msg = `Unable to save libraries: ${err?.message || err}`;
    console.error(err);
    updateLibraryTask(localTaskId, msg, 100, 'failed', taskTitle);
    setStatus(msg);
    alert(msg);
  }
}

function setupLibraryFolderBrowse() {
  const bind = (buttonId, inputId) => {
    const browseBtn = $(buttonId);
    if (!browseBtn) return;
    browseBtn.addEventListener('click', e => {
      e.preventDefault();
      openFolderBrowser(inputId);
    });
  };
  bind('libraryFolderBrowse', 'libraryFolderInput');
  bind('libraryPathBrowse', 'libraryPathInput');
}

function defaultDockerLibraryPath() {
  return '/app/data/library';
}

function setFolderBrowseStatus(text = '', tone = '') {
  const status = $('folderBrowseStatus');
  if (!status) return;
  status.textContent = text || '';
  status.dataset.tone = tone || '';
}

function folderBrowseRootButton(path, label = '') {
  const clean = String(path || '').trim();
  if (!clean) return '';
  return `<button class="folder-browse-root" type="button" data-folder-path="${escapeForAttribute(clean)}">${escapeHtml(label || clean)}</button>`;
}

function folderBrowseEntryButton(entry) {
  const path = String(entry?.path || '').trim();
  const name = String(entry?.name || path || '').trim();
  if (!path) return '';
  return `<button class="folder-browse-entry" type="button" data-folder-path="${escapeForAttribute(path)}">
    <span class="folder-browse-icon">▣</span>
    <span>${escapeHtml(name)}</span>
    <small>${escapeHtml(path)}</small>
  </button>`;
}

async function loadFolderBrowserPath(path = '') {
  const targetPath = String(path || state.folderBrowser.currentPath || defaultDockerLibraryPath()).trim();
  const url = `/api/server/directories?path=${encodeURIComponent(targetPath)}`;
  setFolderBrowseStatus('Loading folders...', 'info');
  try {
    const res = await fetch(url, { cache: 'no-store' });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data?.error || `Unable to browse folders. HTTP ${res.status}`);
    const current = String(data.currentPath || targetPath || defaultDockerLibraryPath());
    state.folderBrowser.currentPath = current;
    state.folderBrowser.roots = Array.isArray(data.roots) ? data.roots : [];
    if ($('folderBrowseCurrent')) $('folderBrowseCurrent').textContent = current;
    const quick = $('folderBrowseQuickRoots');
    if (quick) {
      const roots = state.folderBrowser.roots;
      quick.innerHTML = roots.length
        ? roots.map(root => folderBrowseRootButton(root.path, root.label || root.path)).join('')
        : folderBrowseRootButton(defaultDockerLibraryPath(), '/app/data/library');
    }
    const entries = Array.isArray(data.entries) ? data.entries : [];
    const parent = String(data.parentPath || '').trim();
    const list = $('folderBrowseList');
    if (list) {
      const parentMarkup = parent ? folderBrowseEntryButton({ name: '..', path: parent }) : '';
      list.innerHTML = parentMarkup + (entries.map(folderBrowseEntryButton).join('') || '<div class="folder-browse-empty">No child folders found here.</div>');
    }
    setFolderBrowseStatus(data.note || 'Select a folder, then choose Use This Folder.', 'success');
  } catch (err) {
    console.warn('Folder browser failed', err);
    setFolderBrowseStatus(err?.message || 'Unable to browse folders from the server.', 'error');
  }
}

function openFolderBrowser(targetInputId = 'libraryFolderInput') {
  state.folderBrowser.targetInputId = targetInputId;
  const input = $(targetInputId);
  const startingPath = String(input?.value || '').trim() || defaultDockerLibraryPath();
  if ($('folderBrowseDialog')) $('folderBrowseDialog').showModal();
  loadFolderBrowserPath(startingPath);
}

function closeFolderBrowser() {
  try { $('folderBrowseDialog')?.close?.(); } catch {}
}

function useSelectedFolderBrowserPath() {
  const input = $(state.folderBrowser.targetInputId || 'libraryFolderInput');
  if (input) {
    input.value = state.folderBrowser.currentPath || defaultDockerLibraryPath();
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.focus();
  }
  closeFolderBrowser();
}

async function rescanLibrary() {
  const localTaskId = upsertLibraryTask({
    title: 'Library scan',
    message: 'Requesting library scan...',
    progress: 2
  });
  const res = await fetch('/api/library/rescan', { method: 'POST', cache: 'no-store' });
  let data = null;
  try { data = await res.json(); } catch {}
  if (!res.ok) {
    const msg = data?.error || 'Scan failed. Check the terminal output.';
    updateLibraryTask(localTaskId, msg, 100, 'failed', 'Library scan');
    setStatus(msg);
    alert(msg);
    return;
  }
  const taskId = data?.taskId || data?.TaskId || '';
  if (taskId) {
    replaceLibraryTask(localTaskId, {
      id: taskId,
      kind: 'library-scan',
      title: 'Library scan',
      status: 'running',
      message: data?.message || 'Library scan queued.',
      progressPercent: 5,
      updatedAt: new Date().toISOString()
    });
  } else {
    updateLibraryTask(localTaskId, 'Library scan queued.', 5, 'running', 'Library scan');
  }
  await pollTasks(true);
}

async function cleanupLibrary() {
  const localTaskId = upsertLibraryTask({
    title: 'Library cleanup',
    message: 'Requesting safe library cleanup...',
    progress: 2,
    kind: 'library-cleanup'
  });
  const res = await fetch('/api/library/cleanup', { method: 'POST', cache: 'no-store' });
  let data = null;
  try { data = await res.json(); } catch {}
  if (!res.ok) {
    const msg = data?.error || 'Cleanup failed. Check the terminal output.';
    updateLibraryTask(localTaskId, msg, 100, 'failed', 'Library cleanup');
    setStatus(msg);
    alert(msg);
    return;
  }
  const taskId = data?.taskId || data?.TaskId || '';
  if (taskId) {
    replaceLibraryTask(localTaskId, {
      id: taskId,
      kind: 'library-cleanup',
      title: 'Library cleanup',
      status: 'running',
      message: data?.message || 'Library cleanup queued.',
      progressPercent: 5,
      updatedAt: new Date().toISOString()
    });
  } else {
    updateLibraryTask(localTaskId, 'Library cleanup queued.', 5, 'running', 'Library cleanup');
  }
  await pollTasks(true);
}

async function enrichLibraryMetadata() {
  const localTaskId = upsertLibraryTask({
    title: 'Metadata enrichment',
    message: 'Requesting low-priority ComicInfo metadata import...',
    progress: 2,
    kind: 'library-enrichment'
  });
  const res = await fetch('/api/library/enrich-metadata', { method: 'POST', cache: 'no-store' });
  let data = null;
  try { data = await res.json(); } catch {}
  if (!res.ok) {
    const msg = data?.error || 'Metadata enrichment failed. Check the terminal output.';
    updateLibraryTask(localTaskId, msg, 100, 'failed', 'Metadata enrichment');
    setStatus(msg);
    alert(msg);
    return;
  }
  const taskId = data?.taskId || data?.TaskId || '';
  if (taskId) {
    replaceLibraryTask(localTaskId, {
      id: taskId,
      kind: 'library-enrichment',
      title: 'Metadata enrichment',
      status: 'running',
      message: data?.message || 'Metadata enrichment queued.',
      progressPercent: 5,
      updatedAt: new Date().toISOString()
    });
  } else {
    updateLibraryTask(localTaskId, 'Metadata enrichment queued.', 5, 'running', 'Metadata enrichment');
  }
  await pollTasks(true);
}

function applyFilters() {
  const q = ($('search')?.value || '').trim().toLowerCase();
  state.filtered = state.items.filter(item => {
    const matchesFilter = state.filter === 'All Content' || (state.filter === 'Favorites' ? isFavoriteItem(item) : item.kind === state.filter);
    const matchesCategory = itemMatchesCategoryFilter(item);
    const haystack = [item.title, item.kind, item.system, categoryOf(item), item.publisher, item.year, item.series, item.writer, item.issueNumber, item.asin, item.isbn10, item.isbn13, item.languageTag, platformListText(item), item.platformMatchTitle, item.platformResolverSource, item.summary, item.notes, item.relativePath, item.manualTitle, item.manualType, item.controlScheme, item.warrantySupport, ...(item.includedSections || []), ...(item.itemsCovered || []), ...(item.tags || [])].join(' ').toLowerCase();
    const matchesCustom = !state.customFilter || customSideNavItemMatches(item, state.customFilter);
    return matchesFilter && matchesCategory && matchesCustom && (!q || haystack.includes(q));
  });

  const sort = $('sort')?.value || 'recent';
  state.filtered.sort((a, b) => {
    const pinned = compareItemsByPinnedCategory(a, b);
    // Manuals should behave like reference shelves, not magazine/comic runs.
    // Keep them alphabetical by title unless the user is looking at a mixed/all-content page.
    if (state.filter === 'Manual' || state.viewMode === 'manuals') return pinned || a.title.localeCompare(b.title);
    if (sort === 'title') return pinned || a.title.localeCompare(b.title);
    if (sort === 'kind') return a.kind.localeCompare(b.kind) || compareCategoryNames(a.kind, categoryOf(a), categoryOf(b)) || itemSequenceThenTitle(a, b);
    if (sort === 'category') return pinned || itemSequenceThenTitle(a, b);
    return pinned || (new Date(b.modified) - new Date(a.modified));
  });

  if (state.categoryFilter) {
    if (state.filter === 'Manual') state.filtered.sort((a,b) => a.title.localeCompare(b.title));
    if (state.filter === 'Magazine' || state.filter === 'Strategy Guide') state.filtered.sort(itemSequenceThenTitle);
  }
  if (state.customFilter?.sortMode && state.customFilter.sortMode !== 'default') {
    if (state.customFilter.sortMode === 'title') state.filtered.sort((a,b) => displayTitle(a).localeCompare(displayTitle(b)));
    if (state.customFilter.sortMode === 'sequence') state.filtered.sort(itemSequenceThenTitle);
    if (state.customFilter.sortMode === 'recent') state.filtered.sort((a,b) => new Date(b.modified) - new Date(a.modified));
  }
  render();
}
function magazineThenTitle(a,b){ return itemSequenceThenTitle(a,b); }
function itemSequenceThenTitle(a,b){
  const bothSequenced = a.kind !== 'Manual' && b.kind !== 'Manual' && hasSequence(a) && hasSequence(b);
  return bothSequenced && issueValue(a) !== issueValue(b) ? issueValue(a)-issueValue(b) : a.title.localeCompare(b.title);
}
function count(kind) { return state.items.filter(i => i.kind === kind).length; }
function coverUrl(item) {
  const id = encodeURIComponent(item?.id || item?.Id || '');
  const modified = item?.modified || item?.Modified || '';
  const size = item?.sizeBytes || item?.SizeBytes || '';
  const stamp = modified || size;
  return stamp ? `/api/items/${id}/cover?v=${encodeURIComponent(stamp)}` : `/api/items/${id}/cover`;
}

function coverRetryUrl(baseUrl) {
  const separator = String(baseUrl || '').includes('?') ? '&' : '?';
  return `${baseUrl}${separator}retry=${Date.now()}`;
}

function scheduleCoverRetry(img) {
  if (!img) return;
  const baseUrl = img.dataset.coverSrc || img.src;
  const attempts = Number(img.dataset.coverAttempts || '0') || 0;
  if (!baseUrl || attempts >= 4) return;
  img.dataset.coverAttempts = String(attempts + 1);
  img.classList.add('cover-loading');
  img.classList.remove('cover-loaded', 'cover-error');
  const delay = 900 + attempts * 1300;
  window.setTimeout(() => {
    if (!img.isConnected) return;
    img.src = coverRetryUrl(baseUrl);
  }, delay);
}

function forceCoverRepaint(img) {
  if (!img || !img.isConnected) return;
  // Chromium can occasionally keep archive-backed images as a black composited tile
  // until a hover/transform invalidates the card.  Toggle a tiny, harmless repaint
  // flag after decode/load so the cover paints immediately and persists while scrolling.
  img.classList.add('cover-loaded');
  img.classList.remove('cover-loading', 'cover-error');
  img.style.setProperty('--gv-cover-paint-nonce', String(Date.now() % 100000));
  requestAnimationFrame(() => {
    if (!img.isConnected) return;
    img.classList.add('cover-repaint-pulse');
    void img.offsetHeight;
    requestAnimationFrame(() => img.classList.remove('cover-repaint-pulse'));
  });
}

function primeCoverImage(img) {
  if (!img || !img.dataset.coverSrc) return;
  const wanted = img.dataset.coverSrc;
  const current = img.getAttribute('src') || '';
  img.loading = 'lazy';
  img.decoding = 'async';
  img.classList.add('cover-loading');
  if (current !== wanted) {
    img.setAttribute('src', wanted);
    return;
  }
  if (img.complete && img.naturalWidth > 1 && img.naturalHeight > 1) {
    forceCoverRepaint(img);
    return;
  }
  if (typeof img.decode === 'function') {
    img.decode().then(() => forceCoverRepaint(img)).catch(() => {});
  }
}

let coverPrimeObserver = null;
function ensureCoverPrimeObserver() {
  if (coverPrimeObserver || typeof IntersectionObserver !== 'function') return coverPrimeObserver;
  coverPrimeObserver = new IntersectionObserver(entries => {
    entries.forEach(entry => {
      if (entry.isIntersecting || entry.intersectionRatio > 0) primeCoverImage(entry.target);
    });
  }, { root: libraryScrollElement(), rootMargin: '180px 0px 180px 0px', threshold: 0.01 });
  return coverPrimeObserver;
}

function primeVisibleCoverImages(root = document) {
  const scroller = libraryScrollElement();
  const scrollerRect = scroller?.getBoundingClientRect?.();
  root.querySelectorAll?.('img[data-cover-src]').forEach(img => {
    const rect = img.getBoundingClientRect?.();
    if (!rect || !scrollerRect) { primeCoverImage(img); return; }
    const near = rect.bottom >= scrollerRect.top - 180 && rect.top <= scrollerRect.bottom + 180;
    if (near) primeCoverImage(img);
  });
}

function initializeCoverImages(root = document) {
  const observer = ensureCoverPrimeObserver();
  root.querySelectorAll?.('img[data-cover-src]').forEach(img => {
    if (img.dataset.coverWatch !== '1') {
      img.dataset.coverWatch = '1';
      img.loading = 'lazy';
      img.decoding = 'async';
      img.classList.add('cover-loading');
      img.addEventListener('error', () => {
        img.classList.add('cover-error');
        img.classList.remove('cover-loaded', 'cover-loading');
        scheduleCoverRetry(img);
      });
      img.addEventListener('load', () => {
        const current = String(img.currentSrc || img.src || '').toLowerCase();
        if (current.includes('/assets/missing-cover.svg')) {
          return;
        }
        if (img.naturalWidth > 1 && img.naturalHeight > 1) forceCoverRepaint(img);
      });
      observer?.observe(img);
    }
  });
  requestAnimationFrame(() => primeVisibleCoverImages(root));
}
const ALPHA_RAIL_KEYS = ['#', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'];

function alphaLabel(value) {
  return String(value || '')
    .trim()
    .replace(/^(the|a|an)\s+/i, '')
    .replace(/^[^a-z0-9]+/i, '') || String(value || '').trim();
}
function alphaKey(value) {
  const first = alphaLabel(value).charAt(0).toUpperCase();
  return /^[A-Z]$/.test(first) ? first : '#';
}
function currentCategoryName() {
  if (!state.categoryFilter) return '';
  const parts = String(state.categoryFilter).split('::');
  return parts.length > 1 ? parts.slice(1).join('::') : state.categoryFilter;
}
function libraryScrollElement() {
  return $('libraryGridScroll') || document.querySelector('.main');
}
function scrollMainToTop() {
  const scroller = libraryScrollElement();
  if (scroller) scroller.scrollTo({ top: 0, behavior: 'auto' });
}

let coverPrimeScrollAttached = false;
function attachCoverPrimeScrollHandler() {
  if (coverPrimeScrollAttached) return;
  coverPrimeScrollAttached = true;
  const attach = () => {
    const scroller = libraryScrollElement();
    if (!scroller || scroller.dataset.coverPrimeScroll === '1') return;
    scroller.dataset.coverPrimeScroll = '1';
    let queued = false;
    scroller.addEventListener('scroll', () => {
      if (queued) return;
      queued = true;
      requestAnimationFrame(() => {
        queued = false;
        primeVisibleCoverImages(document);
      });
    }, { passive: true });
  };
  attach();
  window.setTimeout(attach, 300);
}
function renderAlphaRail(labels = []) {
  const rail = $('alphaRail');
  if (!rail) return;
  const cleanLabels = labels.map(v => String(v || '').trim()).filter(Boolean);
  const available = new Set(cleanLabels.map(alphaKey));
  rail.classList.toggle('hidden', !cleanLabels.length);
  rail.innerHTML = ALPHA_RAIL_KEYS.map(key => {
    const enabled = available.has(key);
    const label = key === '#' ? 'Numbers / symbols' : `Jump to ${key}`;
    return `<button type="button" class="alpha-jump${enabled ? '' : ' disabled'}" data-alpha="${key}" title="${label}" aria-label="${label}" ${enabled ? '' : 'disabled'}>${key}</button>`;
  }).join('');
  rail.querySelectorAll('.alpha-jump:not(:disabled)').forEach(btn => btn.addEventListener('click', () => jumpToAlpha(btn.dataset.alpha)));
}
function jumpToAlpha(key) {
  const grid = $('grid');
  const scroller = libraryScrollElement();
  if (!grid || !scroller) return;
  const target = [...grid.querySelectorAll('[data-alpha]')].find(el => el.dataset.alpha === key);
  if (!target) return;
  const scrollerRect = scroller.getBoundingClientRect();
  const targetRect = target.getBoundingClientRect();
  const top = targetRect.top - scrollerRect.top + scroller.scrollTop - 10;
  scroller.scrollTo({ top: Math.max(0, top), behavior: 'smooth' });
}

function render() {
  $('countAll').textContent = state.items.length;
  $('countManuals').textContent = count('Manual');
  $('countGuides').textContent = count('Strategy Guide');
  $('countMags').textContent = count('Magazine');
  if (!state.favorites) loadFavorites();
  $('countFavs').textContent = Object.keys(state.favorites || {}).length;
  const groupMode = ['manual-systems', 'guide-systems', 'magazine-series'].includes(state.viewMode);
  const categoryMode = !!state.categoryFilter;

  $('itemCount').textContent = groupMode ? `${groupCountForView()} categories` : `${state.filtered.length} items`;
  $('libraryView').classList.toggle('category-mode', categoryMode || groupMode || state.viewMode !== 'all');
  $('libraryView').classList.toggle('group-mode', groupMode);
  $('libraryView').classList.toggle('magazine-mode', state.filter === 'Magazine' || state.viewMode === 'magazine-series');

  $('pageTitle').textContent = pageTitleForView();
  $('gridTitle').textContent = groupMode ? pageTitleForView() : (state.customFilter ? `${pageTitleForView()} Results` : (categoryMode ? `${currentCategoryName()} Library` : 'Home Library'));
  $('manualSummary').textContent = `${count('Manual')} items`;
  $('guideSummary').textContent = `${count('Strategy Guide')} items`;
  $('magSummary').textContent = `${count('Magazine')} items`;
  renderCategories();

  const homeMode = !groupMode && !categoryMode && state.viewMode === 'all' && state.filter === 'All Content';
  if ($('homeShelves')) $('homeShelves').classList.toggle('hidden', !homeMode);
  if (groupMode) {
    renderGroupGrid('grid', state.viewMode);
    if ($('recentGrid')) $('recentGrid').innerHTML = '';
    if ($('homeShelves')) $('homeShelves').innerHTML = '';
  } else {
    if (homeMode) renderHomeShelves(); else if ($('homeShelves')) $('homeShelves').innerHTML = '';
    if ($('recentGrid')) $('recentGrid').innerHTML = '';
    renderGrid('grid', state.filtered);
    renderAlphaRail(state.filtered.map(displayTitle));
  }
  installLibraryCardDelegates();
  updateSettingsInsights();
  if (state.selected && $('detailView') && !$('detailView').classList.contains('hidden')) renderDetails(state.selected);
}

function pageTitleForView() {
  if (state.customFilter) return state.customFilter.label || 'Custom List';
  if (state.categoryFilter) return currentCategoryName() || categoryOf(state.filtered[0] || state.selected || { category: 'Category' });
  if (state.viewMode === 'manuals') return 'Manuals';
  if (state.viewMode === 'strategy-guides') return 'Strategy Guides';
  if (state.viewMode === 'magazines') return 'Magazines';
  if (state.viewMode === 'manual-systems') return 'Manuals';
  if (state.viewMode === 'guide-systems') return 'Strategy Guides';
  if (state.viewMode === 'magazine-series') return 'Magazines';
  if (state.viewMode === 'favorites' || state.filter === 'Favorites') return 'Favorites';
  return state.filter === 'All Content' ? 'Home' : (state.filter || 'Home');
}

function groupDefinition(viewMode) {
  if (viewMode === 'guide-systems') return { kind: 'Strategy Guide', empty: 'No guide systems found yet.' };
  if (viewMode === 'magazine-series') return { kind: 'Magazine', empty: 'No magazine series found yet.' };
  return { kind: 'Manual', empty: 'No manual systems found yet.' };
}

function groupCountForView() {
  const def = groupDefinition(state.viewMode);
  return new Set(state.items.filter(i => i.kind === def.kind).flatMap(libraryCategoryKeysForItem)).size;
}

function groupAxisLabelForKind(kind) {
  if (kind === 'Magazine') return 'publication';
  return 'platform';
}
function groupCardSecondaryLabel(kind, name, items) {
  if (kind === 'Magazine') {
    const years = [...new Set(items.map(i => String(i.year || i.coverDate || '').trim()).filter(Boolean))].sort();
    return years.length ? `${years[0]}${years.length > 1 ? ` – ${years[years.length - 1]}` : ''}` : 'Magazine run';
  }
  if (name === MULTI_PLATFORM_LABEL) return 'Appears across associated platforms';
  if (/unsorted/i.test(name)) return 'Needs preferred platform cleanup';
  return `${name} library`;
}
function groupCardLatestLabel(items) {
  const latest = [...items].sort((a,b) => new Date(b.added || b.modified || 0) - new Date(a.added || a.modified || 0))[0];
  return latest ? displayTitle(latest) : 'No recent entry';
}
function groupCardSizeLabel(items) {
  const total = items.reduce((sum, item) => sum + Number(item.sizeBytes || item.size || 0), 0);
  if (!total) return 'Size unknown';
  const units = ['B','KB','MB','GB','TB'];
  let value = total;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) { value /= 1024; unit += 1; }
  return `${value >= 10 || unit === 0 ? Math.round(value) : value.toFixed(1)} ${units[unit]}`;
}
function categoryPreviewCovers(items, name) {
  const covers = items.slice(0, 4);
  if (!covers.length) {
    return `<div class="category-preview-empty">${platformIconHtml(name, 'platform-icon large')}</div>`;
  }
  return covers.map((item, index) => `<img decoding="async" loading="lazy" data-cover-src="${coverUrl(item)}" src="/assets/missing-cover.svg" alt="${escapeHtml(displayTitle(item))} cover" style="--slot:${index}" />`).join('');
}
function renderGroupGrid(id, viewMode) {
  const def = groupDefinition(viewMode);
  const axis = groupAxisLabelForKind(def.kind);
  const allKindItems = state.items.filter(i => i.kind === def.kind);
  const groups = sortCategoriesForKind(def.kind, [...new Set(allKindItems.flatMap(libraryCategoryKeysForItem))]);
  renderAlphaRail(groups);
  const totalSizeLabel = groupCardSizeLabel(allKindItems);
  const overview = groups.length ? `<section class="group-hub-panel">
      <div class="group-hub-copy">
        <span>${escapeHtml(def.kind)} Library</span>
        <h2>Browse by ${escapeHtml(axis === 'publication' ? 'publication' : 'platform')}</h2>
        <p>${escapeHtml(def.kind === 'Magazine'
          ? 'Magazine runs are grouped by publication so issues stay together and remain easier to scan.'
          : 'Content is grouped by platform so each library tile opens a focused shelf of related entries.')}</p>
      </div>
      <div class="group-hub-stats">
        <div><strong>${allKindItems.length}</strong><span>${allKindItems.length === 1 ? 'entry' : 'entries'}</span></div>
        <div><strong>${groups.length}</strong><span>${groups.length === 1 ? axis : `${axis}s`}</span></div>
        <div><strong>${escapeHtml(totalSizeLabel)}</strong><span>library size</span></div>
      </div>
    </section>` : '';
  const cards = groups.map(name => {
    const items = allKindItems.filter(i => libraryCategoryKeysForItem(i).some(c => c.localeCompare(name, undefined, { sensitivity: 'accent' }) === 0)).sort(def.kind === 'Manual' ? ((a,b)=>a.title.localeCompare(b.title)) : itemSequenceThenTitle);
    const issueHint = def.kind === 'Magazine' ? sequenceRange(items) : `${items.length} ${items.length === 1 ? 'entry' : 'entries'}`;
    const specialCategoryClass = def.kind === 'Manual' && isNintendoEntertainmentSystemName(name) ? ' nes-manual-category' : '';
    const latest = groupCardLatestLabel(items);
    const secondary = groupCardSecondaryLabel(def.kind, name, items);
    return `<article class="category-card category-card-redesign${specialCategoryClass}" data-kind="${escapeHtml(def.kind)}" data-category="${escapeForAttribute(name)}" data-alpha="${alphaKey(name)}">
      <div class="category-card-content">
        <div class="category-title-line">
          <span class="category-platform-mark">${platformIconHtml(name, 'platform-icon large')}</span>
          <div>
            <h3 class="category-title"><span>${escapeHtml(name)}</span></h3>
            <p>${escapeHtml(secondary)}</p>
          </div>
        </div>
        <div class="category-card-facts">
          <span>${escapeHtml(issueHint)}</span>
          <span>${escapeHtml(groupCardSizeLabel(items))}</span>
        </div>
        <small class="category-latest">Latest: ${escapeHtml(latest)}</small>
      </div>
      <div class="category-preview-strip" aria-hidden="true">${categoryPreviewCovers(items, name)}</div>
    </article>`;
  }).join('');
  $(id).innerHTML = overview + (cards || `<div class="empty-message">${def.empty} Set a Library Root folder in Settings and scan your collection.</div>`);
  initializeCoverImages($(id));
  attachCoverPrimeScrollHandler();
  $(id).querySelectorAll('.category-card').forEach(card => card.addEventListener('click', () => {
    showLibraryScreen();
    state.filter = card.dataset.kind;
    state.categoryFilter = `${card.dataset.kind}::${card.dataset.category}`;
    state.viewMode = 'category';
    $('search').value = '';
    updateNavActive();
    scrollMainToTop();
    applyFilters();
  }));
}

function sequenceRange(items) {
  const values = items.map(issueValue).filter(n => Number.isFinite(n) && n > 0).sort((a,b)=>a-b);
  if (!values.length) return `${items.length} ${items.length === 1 ? 'issue' : 'issues'}`;
  const first = values[0]; const last = values[values.length - 1];
  return first === last ? `Issue #${first}` : `Issues #${first}–${last}`;
}
function normalizeCategoryStructure(value) {
  return ['content-type', 'platform', 'publisher', 'decade'].includes(value) ? value : 'content-type';
}
function loadCategoryStructure() {
  try { state.categoryStructure = normalizeCategoryStructure(localStorage.getItem(GUIDEVAULT_CATEGORY_STRUCTURE_KEY) || state.categoryStructure || 'content-type'); } catch { state.categoryStructure = normalizeCategoryStructure(state.categoryStructure); }
  if ($('categoryStructureSelect')) $('categoryStructureSelect').value = state.categoryStructure;
  return state.categoryStructure;
}
function saveCategoryStructure(value) {
  state.categoryStructure = normalizeCategoryStructure(value);
  try { localStorage.setItem(GUIDEVAULT_CATEGORY_STRUCTURE_KEY, state.categoryStructure); } catch {}
  if ($('categoryStructureSelect')) $('categoryStructureSelect').value = state.categoryStructure;
}
function isCategoryGroupCollapsed(key) {
  return state.collapsedCategoryGroups?.[key] !== false;
}
function categoryGroupMarkup(key, label, items, categories, options = {}) {
  const collapsed = isCategoryGroupCollapsed(key);
  const groupKind = options.groupKind || key;
  const countFor = options.countFor || ((category) => items.filter(i => libraryCategoryKeysForItem(i).some(name => name.localeCompare(category, undefined, { sensitivity: 'accent' }) === 0)).length);
  const iconFor = options.iconFor || ((category) => platformIconHtml(category));
  const empty = options.empty || '';
  const body = categories.length ? categories.map(c => {
    const category = String(c || '').trim();
    const filterKey = `${groupKind}::${category}`;
    const active = state.categoryFilter === filterKey ? ' active' : '';
    const countForCategory = countFor(category);
    return `<button class="system-btn${active}" data-kind="${escapeHtml(groupKind)}" data-category="${escapeHtml(category)}" title="${escapeHtml(label)}: ${escapeHtml(category)}"><span class="system-label">${iconFor(category)}<span>${escapeHtml(category)}</span></span><em>${countForCategory}</em></button>`;
  }).join('') : `<p class="sub small-pad">${escapeHtml(empty || 'No categories found yet.')}</p>`;
  return `<div class="category-group${collapsed ? ' collapsed' : ''}" data-group-kind="${escapeHtml(key)}">
      <button type="button" class="category-group-toggle" data-kind="${escapeHtml(key)}" aria-expanded="${collapsed ? 'false' : 'true'}" title="${collapsed ? 'Expand' : 'Collapse'} ${escapeHtml(label)}">
        <span class="collapse-mark" aria-hidden="true">${collapsed ? '▸' : '▾'}</span>
        <span class="category-group-label">${escapeHtml(label)}</span>
        <em>${items.length}</em>
      </button>
      <div class="category-body">${body}</div>
    </div>`;
}
function renderCategories() {
  const host = $('categories');
  if (!host) return;
  const structure = loadCategoryStructure();
  const groups = [['Manual', 'Manuals'], ['Strategy Guide', 'Strategy Guides'], ['Magazine', 'Magazines']];
  let markup = '';
  if (structure === 'content-type') {
    markup = groups.map(([kind, label]) => {
      const items = state.items.filter(i => i.kind === kind);
      const categories = sortCategoriesForKind(kind, [...new Set(items.flatMap(libraryCategoryKeysForItem))]);
      if (!categories.length) return '';
      return categoryGroupMarkup(kind, label, items, categories, { groupKind: kind, empty: `No ${label.toLowerCase()} categories found yet.` });
    }).join('');
  } else if (structure === 'platform') {
    const categories = sortCategoriesForKind('Any', [...new Set(state.items.flatMap(libraryCategoryKeysForItem))]);
    markup = categoryGroupMarkup('Any', 'Platforms / Publications', state.items, categories, {
      groupKind: 'Any',
      countFor: category => state.items.filter(i => libraryCategoryKeysForItem(i).some(name => name.localeCompare(category, undefined, { sensitivity: 'accent' }) === 0)).length,
      empty: 'No platforms or publications found yet.'
    });
  } else if (structure === 'publisher') {
    const publishers = [...new Set(state.items.map(i => String(i.publisher || 'Unsorted Publisher').trim()).filter(Boolean))].sort((a,b)=>a.localeCompare(b));
    markup = categoryGroupMarkup('Publisher', 'Publishers', state.items, publishers, {
      groupKind: 'Publisher',
      iconFor: () => '<span class="category-mini-icon" aria-hidden="true">▦</span>',
      countFor: publisher => state.items.filter(i => String(i.publisher || 'Unsorted Publisher').trim().localeCompare(publisher, undefined, { sensitivity: 'accent' }) === 0).length,
      empty: 'No publisher values found yet.'
    });
  } else if (structure === 'decade') {
    const decades = [...new Set(state.items.map(decadeLabelForItem))].sort((a,b) => {
      if (a === 'Unknown Decade') return 1;
      if (b === 'Unknown Decade') return -1;
      return Number.parseInt(a,10) - Number.parseInt(b,10);
    });
    markup = categoryGroupMarkup('Decade', 'Decades', state.items, decades, {
      groupKind: 'Decade',
      iconFor: () => '<span class="category-mini-icon" aria-hidden="true">◷</span>',
      countFor: decade => state.items.filter(i => decadeLabelForItem(i).localeCompare(decade, undefined, { sensitivity: 'accent' }) === 0).length,
      empty: 'No dated entries found yet.'
    });
  }
  host.innerHTML = markup || '<p class="sub small-pad">Scan a library root to build categories.</p>';
  host.querySelectorAll('.category-group-toggle').forEach(btn => btn.addEventListener('click', () => {
    const kind = btn.dataset.kind;
    state.collapsedCategoryGroups[kind] = !isCategoryGroupCollapsed(kind);
    renderCategories();
  }));
  host.querySelectorAll('.system-btn').forEach(btn => btn.addEventListener('click', () => {
    showLibraryScreen();
    const kind = btn.dataset.kind || 'Any';
    state.filter = ['Any', 'Publisher', 'Decade'].includes(kind) ? 'All Content' : kind;
    state.categoryFilter = `${kind}::${btn.dataset.category}`;
    state.customFilter = null;
    state.viewMode = 'category';
    if ($('search')) $('search').value = '';
    updateNavActive();
    document.querySelectorAll('.chip').forEach(c => c.classList.toggle('active', c.dataset.kind === state.filter));
    scrollMainToTop();
    applyFilters();
  }));
}


function renderGrid(id, items) {
  $(id).innerHTML = items.map(item => cardMarkupForItem(item)).join('') || `<div class="empty-message">No content found. Set a Library Root folder in Settings and scan for CBZ, CBR, or PDF files.</div>`;
  initializeCoverImages($(id));
  attachCoverPrimeScrollHandler();
}
function kindClass(kind) { return kind === 'Magazine' ? 'mag' : kind === 'Strategy Guide' ? 'guide' : 'manual'; }
function itemPageCountLabel(item) {
  const count = Number(item?.pageCount ?? item?.PageCount ?? 0) || 0;
  const format = String(item?.format || item?.Format || '').toUpperCase();
  if (format === 'PDF') return 'Browser PDF';
  return count > 0 ? `${count} pages` : `${format || 'Archive'} pages`;
}
function isNintendoEntertainmentSystemName(value) {
  const n = normalizeName(value || '');
  return n === 'nintendo entertainment system' || n === 'nes' || n === 'nintendo nes' || n.includes('nintendo entertainment system');
}
function specialCardClass(item) {
  if (!item) return '';
  const category = categoryOf(item);
  return item.kind === 'Manual' && isNintendoEntertainmentSystemName(category) ? 'nes-manual-card' : '';
}


const KEYBIND_DEFAULTS = [
  { id: 'toggle-fullscreen', title: 'Toggle full screen', description: 'Alternative to F11', keys: ['F'] },
  { id: 'bookmark-current-page', title: 'Bookmark current page', description: 'Saves the current image as a bookmark', keys: ['Ctrl + B'] },
  { id: 'open-help-menu', title: 'Open help menu', description: 'Opens a help modal with all relevant keybinds', keys: ['H'] },
  { id: 'goto-page', title: 'Goto page', description: 'Open a prompt to switch pages', keys: ['G'] },
  { id: 'toggle-menu', title: 'Toggle menu', description: 'Toggles the reader menu', keys: ['Space'] },
  { id: 'page-right', title: 'Page right', description: 'Move one page to the right', keys: ['→'] },
  { id: 'page-left', title: 'Page left', description: 'Move one page to the left', keys: ['←'] },
  { id: 'page-up', title: 'Page up', description: 'Move one page upwards', keys: ['↑'] },
  { id: 'page-down', title: 'Page down', description: 'Move one page downwards', keys: ['↓'] },
  { id: 'offset-double-page', title: 'Offset double page', description: 'Offset pages for double page spread alignment', keys: ['O'] },
  { id: 'first-page', title: 'First Page', description: 'Move to the first page', keys: ['Ctrl + ←'] },
  { id: 'last-page', title: 'Last Page', description: 'Move to the last page', keys: ['Ctrl + →'] }
];

const HOME_SHELF_PAGE_SIZE = 6;
const HOME_SHELF_MAX_ITEMS = 48;

const HOME_SHELF_OPTIONS = [
  { id: 'recently-added', label: 'Recently Added', description: 'Newest scanned entries across all content.', kind: '' },
  { id: 'recently-viewed', label: 'Recently Viewed', description: 'Entries opened recently in this browser.', kind: '' },
  { id: 'manuals', label: 'Manuals', description: 'Latest manual entries.', kind: 'Manual' },
  { id: 'strategy-guides', label: 'Strategy Guides', description: 'Latest strategy guide entries.', kind: 'Strategy Guide' },
  { id: 'magazines', label: 'Magazines', description: 'Latest magazine entries.', kind: 'Magazine' },
  { id: 'unsorted-strategy-guides', label: 'Unsorted Strategy Guides', description: 'Strategy guides missing a clean preferred platform.', kind: 'Strategy Guide' },
  { id: 'multi-platform-guides', label: 'Multi-Platform Strategy Guides', description: 'Strategy guides with more than one associated platform.', kind: 'Strategy Guide' },
  { id: 'largest-files', label: 'Largest Files', description: 'Largest files in the current index.', kind: '' }
];

function normalizeKeybinds(value = {}) {
  const map = {};
  KEYBIND_DEFAULTS.forEach(def => {
    const saved = value?.[def.id];
    const keys = Array.isArray(saved?.keys) ? saved.keys : Array.isArray(saved) ? saved : def.keys;
    map[def.id] = { keys: [...new Set(keys.map(k => String(k || '').trim()).filter(Boolean))] };
    if (!map[def.id].keys.length) map[def.id].keys = def.keys.slice();
  });
  return map;
}
function loadKeybinds() {
  let parsed = {};
  try { parsed = JSON.parse(localStorage.getItem(GUIDEVAULT_KEYBINDS_KEY) || '{}') || {}; } catch {}
  state.keybinds.bindings = normalizeKeybinds(parsed);
  return state.keybinds.bindings;
}
function saveKeybinds() {
  state.keybinds.bindings = normalizeKeybinds(state.keybinds.bindings || {});
  try { localStorage.setItem(GUIDEVAULT_KEYBINDS_KEY, JSON.stringify(state.keybinds.bindings)); } catch {}
}
function renderKeybindsSettings() {
  const list = $('keybindsList');
  if (!list) return;
  const bindings = state.keybinds.bindings || loadKeybinds();
  list.innerHTML = KEYBIND_DEFAULTS.map(def => {
    const keys = bindings[def.id]?.keys?.length ? bindings[def.id].keys : def.keys;
    return `<div class="keybind-row" data-keybind-id="${escapeForAttribute(def.id)}">
      <div class="keybind-main">
        <h3>${escapeHtml(def.title)}</h3>
        <div class="keybind-chip-line">${keys.map(key => `<span class="keybind-key-chip">${escapeHtml(key)}</span>`).join('')}</div>
        <p class="sub">${escapeHtml(def.description)}</p>
      </div>
      <div class="keybind-actions" aria-label="${escapeForAttribute(def.title)} actions">
        <button class="keybind-action" data-keybind-action="add" type="button" title="Add alternate key" aria-label="Add alternate key">ï¼‹</button>
        <button class="keybind-action" data-keybind-action="reset" type="button" title="Reset binding" aria-label="Reset binding">♻</button>
      </div>
    </div>`;
  }).join('');
}
function setKeybindStatus(text, tone = '') {
  const el = $('keybindsStatus');
  if (!el) return;
  el.textContent = text || '';
  el.dataset.tone = tone || '';
}
function handleKeybindAction(e) {
  const btn = e.target.closest?.('[data-keybind-action]');
  if (!btn) return;
  const row = btn.closest('.keybind-row');
  const id = row?.dataset?.keybindId || '';
  const def = KEYBIND_DEFAULTS.find(x => x.id === id);
  if (!def) return;
  const action = btn.dataset.keybindAction;
  const bindings = state.keybinds.bindings || loadKeybinds();
  if (action === 'reset') {
    bindings[id] = { keys: def.keys.slice() };
    saveKeybinds();
    renderKeybindsSettings();
    setKeybindStatus(`Reset ${def.title} to the default binding.`, 'success');
    return;
  }
  if (action === 'add') {
    const next = prompt(`Add an alternate key for ${def.title}. Example: Ctrl + Alt + R`, '');
    const clean = String(next || '').trim();
    if (!clean) return;
    const keys = bindings[id]?.keys?.length ? bindings[id].keys.slice() : def.keys.slice();
    if (!keys.some(k => k.localeCompare(clean, undefined, { sensitivity: 'accent' }) === 0)) keys.push(clean);
    bindings[id] = { keys };
    saveKeybinds();
    renderKeybindsSettings();
    setKeybindStatus(`Added ${clean} as an alternate binding for ${def.title}.`, 'success');
  }
}
function resetAllKeybinds() {
  state.keybinds.bindings = normalizeKeybinds({});
  saveKeybinds();
  renderKeybindsSettings();
  setKeybindStatus('All key binds reset to their default values.', 'success');
}

function isReaderActiveForKeybinds() {
  const reader = $('readerView');
  return !!reader && !reader.classList.contains('hidden') && !!state.reader.item && Array.isArray(state.reader.pages) && state.reader.pages.length > 0;
}

function isEditableKeyTarget(target) {
  const el = target?.closest?.('input, textarea, select, button, [contenteditable="true"]');
  return !!el && !el.closest?.('#readerView');
}

function canonicalKeyLabel(label = '') {
  return String(label || '')
    .replace(/arrowright/ig, '→')
    .replace(/arrowleft/ig, '←')
    .replace(/arrowup/ig, '↑')
    .replace(/arrowdown/ig, '↓')
    .replace(/control/ig, 'ctrl')
    .replace(/command|cmd/ig, 'meta')
    .replace(/\s*\+\s*/g, '+')
    .replace(/\s+/g, '')
    .toLowerCase();
}

function keyEventToBindingLabel(e) {
  let key = e.key || '';
  if (key === ' ') key = 'Space';
  if (key === 'ArrowRight') key = '→';
  else if (key === 'ArrowLeft') key = '←';
  else if (key === 'ArrowUp') key = '↑';
  else if (key === 'ArrowDown') key = '↓';
  else if (key.length === 1) key = key.toUpperCase();
  const parts = [];
  if (e.ctrlKey) parts.push('Ctrl');
  if (e.altKey) parts.push('Alt');
  if (e.shiftKey && key.length > 1) parts.push('Shift');
  if (e.metaKey) parts.push('Meta');
  parts.push(key);
  return parts.join(' + ');
}

function readerActionForKeyEvent(e) {
  const pressed = canonicalKeyLabel(keyEventToBindingLabel(e));
  const bindings = state.keybinds.bindings || loadKeybinds();
  for (const def of KEYBIND_DEFAULTS) {
    const keys = bindings[def.id]?.keys?.length ? bindings[def.id].keys : def.keys;
    if (keys.some(key => canonicalKeyLabel(key) === pressed)) return def.id;
  }
  return '';
}

async function toggleReaderFullscreenFromKeybind() {
  const el = $('readerStage') || $('readerView');
  try {
    if (!document.fullscreenElement) await el?.requestFullscreen?.();
    else await document.exitFullscreen?.();
  } catch (err) {
    console.warn('Unable to toggle reader fullscreen', err);
  }
  updateReaderFullscreenUi();
}

function showReaderKeybindHelp() {
  const bindings = state.keybinds.bindings || loadKeybinds();
  const lines = KEYBIND_DEFAULTS.map(def => {
    const keys = bindings[def.id]?.keys?.length ? bindings[def.id].keys : def.keys;
    return `${def.title}: ${keys.join(', ')}`;
  });
  setReaderOverlayVisible(true);
  alert(`Guidevault Reader Keybinds\n\n${lines.join('\n')}`);
}

function promptReaderGoToPage() {
  const total = readerPageCount();
  const current = currentReaderPageNumber();
  const value = prompt(`Go to page 1-${total}`, String(current));
  if (value === null) return;
  jumpReaderToPage(value);
}

let readerHitZoneCleanupTimer = null;

function cleanupReaderHitZoneFocus() {
  ['leftHit', 'centerHit', 'rightHit'].forEach(id => {
    const hit = $(id);
    if (!hit) return;
    hit.setAttribute('tabindex', '-1');
    hit.setAttribute('aria-hidden', 'true');
    hit.blur?.();
  });
  const active = document.activeElement;
  if (active && active.closest?.('#readerView')) active.blur?.();
  const reader = $('readerView');
  if (!reader) return;
  reader.classList.add('reader-key-action-cleanup');
  if (readerHitZoneCleanupTimer) window.clearTimeout(readerHitZoneCleanupTimer);
  readerHitZoneCleanupTimer = window.setTimeout(() => {
    reader.classList.remove('reader-key-action-cleanup');
    readerHitZoneCleanupTimer = null;
  }, 220);
}

async function runReaderKeybindAction(action) {
  if (!action) return false;
  switch (action) {
    case 'toggle-fullscreen': await toggleReaderFullscreenFromKeybind(); return true;
    case 'bookmark-current-page': bookmarkCurrentReaderPage(); setReaderOverlayVisible(true); return true;
    case 'open-help-menu': showReaderKeybindHelp(); return true;
    case 'goto-page': promptReaderGoToPage(); return true;
    case 'toggle-menu': toggleReaderOverlay(); return true;
    case 'page-right':
    case 'page-down': await showPage(state.reader.index, 'next'); return true;
    case 'page-left':
    case 'page-up': await showPage(state.reader.index, 'prev'); return true;
    case 'offset-double-page': setReaderDisplayMode(normalizeReaderDisplayMode(state.reader.displayMode) === 2 ? 3 : 2); setReaderOverlayVisible(true); return true;
    case 'first-page': jumpReaderToPage(1); return true;
    case 'last-page': jumpReaderToPage(readerPageCount()); return true;
    default: return false;
  }
}

async function handleReaderKeydown(e) {
  if (!isReaderActiveForKeybinds()) return;
  if (isEditableKeyTarget(e.target)) return;
  if (e.key === 'Escape') return;
  const action = readerActionForKeyEvent(e);
  if (!action) return;
  e.preventDefault();
  e.stopPropagation();
  e.stopImmediatePropagation?.();
  cleanupReaderHitZoneFocus();
  await runReaderKeybindAction(action);
  cleanupReaderHitZoneFocus();
}

function normalizeCustomSideNavItem(value = {}, index = 0) {
  const type = ['series', 'kind', 'category', 'publisher', 'list', 'search'].includes(String(value.type || '').toLowerCase()) ? String(value.type).toLowerCase() : 'series';
  const label = String(value.label || value.value || `Shortcut ${index + 1}`).trim();
  const id = String(value.id || `custom-nav-${Date.now()}-${index}-${Math.random().toString(36).slice(2, 7)}`).trim();
  const kindScopeRaw = String(value.kindScope || value.scope || 'all').trim();
  const kindScope = ['Manual', 'Strategy Guide', 'Magazine'].includes(kindScopeRaw) ? kindScopeRaw : 'all';
  const matchMode = String(value.matchMode || '').toLowerCase() === 'exact' ? 'exact' : 'contains';
  const sortMode = ['default', 'title', 'sequence', 'recent'].includes(String(value.sortMode || '').toLowerCase()) ? String(value.sortMode || '').toLowerCase() : 'default';
  const icon = String(value.icon || value.iconPreset || customSideNavDefaultIcon(type, kindScope)).trim().slice(0, 4);
  return {
    id,
    label: label || `Shortcut ${index + 1}`,
    type,
    value: String(value.value || label || '').trim(),
    kindScope,
    matchMode,
    sortMode,
    icon
  };
}
function normalizeSideNavSettings(value = {}) {
  const items = Array.isArray(value?.customItems) ? value.customItems : [];
  const deduped = [];
  const seen = new Set();
  items.map((item, index) => normalizeCustomSideNavItem(item, index)).forEach(item => {
    const key = `${item.type}::${item.label.toLowerCase()}::${item.value.toLowerCase()}`;
    if (!item.label || !item.value || seen.has(key)) return;
    seen.add(key);
    deduped.push(item);
  });
  return { customItems: deduped };
}
function normalizeCustomize(value = {}) {
  const ids = new Set(HOME_SHELF_OPTIONS.map(s => s.id));
  const shelves = Array.isArray(value?.homeShelves) ? value.homeShelves : ['recently-added'];
  const clean = shelves.map(v => String(v || '').trim()).filter(id => ids.has(id));
  const requestedTab = String(value?.activeTab || 'home');
  const activeTab = requestedTab === 'side-nav' ? 'side-nav' : 'home';
  return {
    activeTab,
    homeShelves: clean.length ? [...new Set(clean)] : ['recently-added'],
    sideNav: normalizeSideNavSettings(value?.sideNav || {})
  };
}
function loadCustomizeSettings() {
  let parsed = {};
  try { parsed = JSON.parse(localStorage.getItem(GUIDEVAULT_CUSTOMIZE_KEY) || '{}') || {}; } catch {}
  state.customize = normalizeCustomize(parsed);
  return state.customize;
}
function saveCustomizeSettings() {
  state.customize = normalizeCustomize(state.customize || {});
  try { localStorage.setItem(GUIDEVAULT_CUSTOMIZE_KEY, JSON.stringify(state.customize)); } catch {}
}
function setCustomizeStatus(text, tone = '') {
  const el = $('customizeStatus');
  if (!el) return;
  el.textContent = text || '';
  el.dataset.tone = tone || '';
}

function setCustomSideNavStatus(text, tone = '') {
  const el = $('customSideNavStatus');
  if (!el) return;
  el.textContent = text || '';
  el.dataset.tone = tone || '';
}
function customSideNavDefaultIcon(type = 'series', kindScope = 'all') {
  if (kindScope === 'Strategy Guide') return '▤';
  if (kindScope === 'Magazine') return '▧';
  if (kindScope === 'Manual') return '▦';
  return ({ series: '▦', kind: '▤', category: '⌘', publisher: '◫', list: '☷', search: '⌕' })[type] || '☷';
}
function customSideNavTypeLabel(type) {
  return ({ series: 'Series', kind: 'Content Type', category: 'Platform', publisher: 'Publisher', list: 'Curated List', search: 'Search' })[type] || 'Shortcut';
}
function customSideNavScopeLabel(scope) {
  return ({ 'Manual': 'Manuals only', 'Strategy Guide': 'Strategy guides only', 'Magazine': 'Magazines only', all: 'All content' })[scope || 'all'] || 'All content';
}
function customSideNavCount(item) {
  return state.items.filter(libraryItem => customSideNavItemMatches(libraryItem, item)).length;
}
function sideNavSearchHaystack(item) {
  return [item.title, item.kind, item.system, categoryOf(item), item.publisher, item.year, item.series, item.writer, item.issueNumber, item.asin, item.isbn10, item.isbn13, item.languageTag, platformListText(item), item.platformMatchTitle, item.platformResolverSource, item.summary, item.notes, item.relativePath, item.manualTitle, item.manualType, item.controlScheme, item.warrantySupport, ...(item.includedSections || []), ...(item.itemsCovered || []), ...(item.tags || [])].join(' ').toLowerCase();
}
function customSideNavItemMatches(item, custom = state.customFilter) {
  if (!custom) return true;
  const scope = custom.kindScope || 'all';
  if (scope !== 'all' && item.kind !== scope) return false;
  const value = String(custom.value || '').trim().toLowerCase();
  if (!value) return true;
  const exact = custom.matchMode === 'exact';
  const matchesText = text => {
    const hay = String(text || '').trim().toLowerCase();
    return exact ? hay === value : hay.includes(value);
  };
  const matchesAny = values => (values || []).some(v => matchesText(v));
  switch (custom.type) {
    case 'series': return matchesText(item.series) || matchesText(item.title) || matchesText(item.manualTitle) || matchesText(item.collectionTitle);
    case 'kind': return exact ? String(item.kind || '').trim().toLowerCase() === value : matchesText(item.kind);
    case 'category': return matchesText(categoryOf(item)) || matchesText(item.system) || matchesAny(associatedPlatformsOf(item));
    case 'publisher': return matchesText(item.publisher);
    case 'list': return matchesText(item.series) || matchesText(item.collectionTitle) || matchesAny(item.tags || []) || matchesAny(item.itemsCovered || []) || matchesText(item.title) || matchesText(item.summary) || matchesText(item.notes);
    case 'search': return exact ? sideNavSearchHaystack(item).split(/\s+/).includes(value) : sideNavSearchHaystack(item).includes(value);
    default: return exact ? false : sideNavSearchHaystack(item).includes(value);
  }
}
function renderCustomSideNavItems() {
  const host = $('customSideNavItems');
  if (!host) return;
  const settings = state.customize || loadCustomizeSettings();
  const items = settings.sideNav?.customItems || [];
  host.innerHTML = items.map(item => `<button class="nav custom-side-nav-button" type="button" data-custom-nav-id="${escapeForAttribute(item.id)}" title="${escapeForAttribute(item.label)}"><span class="nav-icon custom-list-icon">${escapeHtml(item.icon || customSideNavDefaultIcon(item.type, item.kindScope))}</span><span class="nav-label">${escapeHtml(item.label)}</span><em>${customSideNavCount(item)}</em></button>`).join('');
  host.classList.toggle('hidden', !items.length);
  host.querySelectorAll('[data-custom-nav-id]').forEach(btn => btn.classList.toggle('active', state.customFilter?.id === btn.dataset.customNavId));
}
function renderCustomSideNavList() {
  const list = $('customSideNavList');
  if (!list) return;
  const settings = state.customize || loadCustomizeSettings();
  const items = settings.sideNav?.customItems || [];
  list.innerHTML = items.map(item => `<div class="custom-side-nav-row" data-custom-nav-id="${escapeForAttribute(item.id)}"><span class="custom-side-nav-icon">${escapeHtml(item.icon || customSideNavDefaultIcon(item.type, item.kindScope))}</span><div class="custom-side-nav-row-copy"><strong>${escapeHtml(item.label)}</strong><p class="sub">${escapeHtml(customSideNavTypeLabel(item.type))}: ${escapeHtml(item.value)}</p><div class="custom-side-nav-chipline"><span>${escapeHtml(customSideNavScopeLabel(item.kindScope))}</span><span>${item.matchMode === 'exact' ? 'Exact' : 'Contains'}</span><span>${escapeHtml((item.sortMode || 'default').replace(/^./, c => c.toUpperCase()))}</span><span>${customSideNavCount(item)} results</span></div></div><button class="danger" type="button" data-custom-side-nav-action="remove">Remove</button></div>`).join('') || '<p class="sub">No custom shortcuts yet. Add one for a favorite series, magazine run, publisher, platform, or saved search.</p>';
}
function addCustomSideNavItem() {
  const raw = {
    label: $('customSideNavLabel')?.value,
    type: $('customSideNavType')?.value,
    value: $('customSideNavValue')?.value,
    kindScope: $('customSideNavKindScope')?.value,
    matchMode: $('customSideNavMatchMode')?.value,
    sortMode: $('customSideNavSortMode')?.value,
    icon: $('customSideNavIcon')?.value || $('customSideNavIconPreset')?.value
  };
  const item = normalizeCustomSideNavItem(raw, (state.customize?.sideNav?.customItems || []).length);
  if (!item.label || !item.value) { setCustomSideNavStatus('Add a label and match value first.', 'error'); return; }
  const settings = state.customize || loadCustomizeSettings();
  settings.sideNav = normalizeSideNavSettings(settings.sideNav || {});
  settings.sideNav.customItems.push(item);
  state.customize = settings;
  saveCustomizeSettings();
  ['customSideNavLabel','customSideNavValue','customSideNavIcon'].forEach(id => { if ($(id)) $(id).value = ''; });
  if ($('customSideNavKindScope')) $('customSideNavKindScope').value = 'all';
  if ($('customSideNavMatchMode')) $('customSideNavMatchMode').value = 'contains';
  if ($('customSideNavSortMode')) $('customSideNavSortMode').value = 'default';
  if ($('customSideNavIconPreset')) $('customSideNavIconPreset').value = '☷';
  renderCustomizeSettings();
  setCustomSideNavStatus('Side nav shortcut added.', 'success');
}
function removeCustomSideNavItem(id) {
  const settings = state.customize || loadCustomizeSettings();
  settings.sideNav = normalizeSideNavSettings(settings.sideNav || {});
  settings.sideNav.customItems = settings.sideNav.customItems.filter(item => item.id !== id);
  if (state.customFilter?.id === id) state.customFilter = null;
  saveCustomizeSettings();
  renderCustomizeSettings();
  updateNavActive();
  applyFilters();
}
function resetCustomSideNavItems() {
  const settings = state.customize || loadCustomizeSettings();
  settings.sideNav = { customItems: [] };
  state.customFilter = null;
  saveCustomizeSettings();
  renderCustomizeSettings();
  updateNavActive();
  applyFilters();
  setCustomSideNavStatus('Custom side nav shortcuts reset.', 'info');
}
function applyCustomSideNavItem(id) {
  const settings = state.customize || loadCustomizeSettings();
  const item = (settings.sideNav?.customItems || []).find(x => x.id === id);
  if (!item) return;
  showLibraryScreen();
  state.customFilter = item;
  state.viewMode = 'custom';
  state.filter = 'All Content';
  state.categoryFilter = '';
  if ($('search') && item.type !== 'search') $('search').value = '';
  document.querySelectorAll('.chip').forEach(c => c.classList.remove('active'));
  updateNavActive();
  scrollMainToTop();
  applyFilters();
}
function handleCustomSideNavListAction(e) {
  const btn = e.target.closest?.('[data-custom-side-nav-action]');
  if (!btn) return;
  const row = btn.closest('[data-custom-nav-id]');
  if (btn.dataset.customSideNavAction === 'remove') removeCustomSideNavItem(row?.dataset.customNavId || '');
}
function renderCustomizeSettings() {
  const settings = state.customize || loadCustomizeSettings();
  const select = $('customizeShelfSelect');
  if (select) {
    const active = new Set(settings.homeShelves || []);
    select.innerHTML = HOME_SHELF_OPTIONS.map(opt => `<option value="${escapeForAttribute(opt.id)}" ${active.has(opt.id) ? 'disabled' : ''}>${escapeHtml(opt.label)}</option>`).join('');
  }
  const list = $('customizeShelfList');
  if (list) {
    list.innerHTML = (settings.homeShelves || []).map((id, index) => {
      const opt = HOME_SHELF_OPTIONS.find(o => o.id === id) || HOME_SHELF_OPTIONS[0];
      return `<div class="customize-shelf-row" data-shelf-id="${escapeForAttribute(id)}">
        <span class="customize-shelf-handle">⠿</span>
        <div><strong>${escapeHtml(opt.label)}</strong><p class="sub">${escapeHtml(opt.description)}</p></div>
        <div class="customize-shelf-actions">
          <button class="ghost" data-shelf-action="up" type="button" ${index === 0 ? 'disabled' : ''}>↑</button>
          <button class="ghost" data-shelf-action="down" type="button" ${index === settings.homeShelves.length - 1 ? 'disabled' : ''}>↓</button>
          <button class="danger" data-shelf-action="remove" type="button">Remove</button>
        </div>
      </div>`;
    }).join('') || '<p class="sub">No Home shelves selected. Add a shelf to populate Home above the main grid.</p>';
  }
  renderCustomSideNavItems();
  renderCustomSideNavList();
  document.querySelectorAll('.customize-tab').forEach(btn => btn.classList.toggle('active', (btn.dataset.customizeTab || 'home') === (settings.activeTab || 'home')));
  const tabIds = { home: 'customizeHomePanel', 'side-nav': 'customizeSideNavPanel' };
  Object.entries(tabIds).forEach(([key, id]) => $(id)?.classList.toggle('hidden', (settings.activeTab || 'home') !== key));
}
function handleCustomizeTabClick(e) {
  const btn = e.target.closest?.('.customize-tab');
  if (!btn) return;
  state.customize.activeTab = btn.dataset.customizeTab || 'home';
  saveCustomizeSettings();
  renderCustomizeSettings();
}
function addCustomizeShelf() {
  const select = $('customizeShelfSelect');
  const id = select?.value || HOME_SHELF_OPTIONS.find(opt => !(state.customize.homeShelves || []).includes(opt.id))?.id || '';
  if (!id) return;
  const settings = state.customize || loadCustomizeSettings();
  if (!settings.homeShelves.includes(id)) settings.homeShelves.push(id);
  saveCustomizeSettings();
  renderCustomizeSettings();
  render();
  setCustomizeStatus('Home shelf added.', 'success');
}
function handleCustomizeShelfAction(e) {
  const btn = e.target.closest?.('[data-shelf-action]');
  if (!btn) return;
  const row = btn.closest('.customize-shelf-row');
  const id = row?.dataset?.shelfId || '';
  const shelves = (state.customize.homeShelves || []).slice();
  const index = shelves.indexOf(id);
  if (index < 0) return;
  const action = btn.dataset.shelfAction;
  if (action === 'remove') shelves.splice(index, 1);
  if (action === 'up' && index > 0) [shelves[index - 1], shelves[index]] = [shelves[index], shelves[index - 1]];
  if (action === 'down' && index < shelves.length - 1) [shelves[index + 1], shelves[index]] = [shelves[index], shelves[index + 1]];
  state.customize.homeShelves = shelves;
  saveCustomizeSettings();
  renderCustomizeSettings();
  render();
  setCustomizeStatus('Home shelf layout updated.', 'success');
}

function readReadingActivity() {
  try { return JSON.parse(localStorage.getItem(GUIDEVAULT_READING_ACTIVITY_KEY) || '[]').filter(Boolean); } catch { return []; }
}
function saveReadingActivity(events) {
  const clean = Array.isArray(events) ? events.slice(-500) : [];
  try { localStorage.setItem(GUIDEVAULT_READING_ACTIVITY_KEY, JSON.stringify(clean)); } catch {}
}
function recordReadingActivity(item, action = 'view') {
  if (!item) return;
  const profile = state.auth.profile || readLoginProfile() || {};
  const events = readReadingActivity();
  events.push({
    id: item.id || item.Id || '',
    title: displayTitle(item) || item.title || '',
    kind: item.kind || '',
    format: item.format || item.Format || fileExtensionOf(item).replace('.', '').toUpperCase(),
    user: profile.username || profile.email || 'local user',
    action,
    at: new Date().toISOString()
  });
  saveReadingActivity(events);
}
function shelfItemsFor(id, items = state.items, limit = HOME_SHELF_MAX_ITEMS) {
  const all = Array.isArray(items) ? items.slice() : [];
  const byRecent = (a,b) => dateValue(b.modified || b.Modified || b.addedAt || b.createdAt || 0) - dateValue(a.modified || a.Modified || a.addedAt || a.createdAt || 0) || displayTitle(a).localeCompare(displayTitle(b));
  if (id === 'recently-added') return all.sort(byRecent).slice(0, limit);
  if (id === 'manuals') return all.filter(i => i.kind === 'Manual').sort(byRecent).slice(0, limit);
  if (id === 'strategy-guides') return all.filter(i => i.kind === 'Strategy Guide').sort(byRecent).slice(0, limit);
  if (id === 'magazines') return all.filter(i => i.kind === 'Magazine').sort(byRecent).slice(0, limit);
  if (id === 'unsorted-strategy-guides') return all.filter(i => i.kind === 'Strategy Guide' && isBlankish(preferredPlatformOf(i))).sort((a,b)=>displayTitle(a).localeCompare(displayTitle(b))).slice(0, limit);
  if (id === 'multi-platform-guides') return all.filter(i => i.kind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(i)).sort((a,b)=>displayTitle(a).localeCompare(displayTitle(b))).slice(0, limit);
  if (id === 'largest-files') return all.sort((a,b)=>(Number(b.sizeBytes||b.SizeBytes||0)-Number(a.sizeBytes||a.SizeBytes||0))).slice(0, limit);
  if (id === 'recently-viewed') {
    const lookup = new Map(all.map(item => [String(item.id || item.Id), item]));
    const seenIds = readReadingActivity().slice().reverse().map(e => e.id).filter(Boolean);
    const unique = [...new Set(seenIds)];
    return unique.map(itemId => lookup.get(String(itemId))).filter(Boolean).slice(0, limit);
  }
  return [];
}
function renderHomeShelves() {
  const host = $('homeShelves');
  if (!host) return;
  const settings = state.customize || loadCustomizeSettings();
  const shelves = settings.homeShelves?.length ? settings.homeShelves : ['recently-added'];
  state.homeShelfOffsets = state.homeShelfOffsets || {};
  state.homeShelfSlideDirection = state.homeShelfSlideDirection || {};
  host.innerHTML = shelves.map(id => {
    const opt = HOME_SHELF_OPTIONS.find(o => o.id === id) || HOME_SHELF_OPTIONS[0];
    const items = shelfItemsFor(id, state.items);
    const maxOffset = Math.max(0, items.length - HOME_SHELF_PAGE_SIZE);
    const offset = Math.min(Math.max(0, Number(state.homeShelfOffsets[id] || 0)), maxOffset);
    state.homeShelfOffsets[id] = offset;
    const visible = items.slice(offset, offset + HOME_SHELF_PAGE_SIZE);
    const hasPages = items.length > HOME_SHELF_PAGE_SIZE;
    const rangeText = items.length ? `${offset + 1}-${Math.min(offset + HOME_SHELF_PAGE_SIZE, items.length)} of ${items.length}` : '0 items';
    const slideDirection = Number(state.homeShelfSlideDirection[id] || 0);
    const slideClass = slideDirection > 0 ? ' slide-in-from-right' : slideDirection < 0 ? ' slide-in-from-left' : '';
    return `<section class="home-shelf" data-home-shelf="${escapeForAttribute(id)}">
      <div class="home-shelf-heading">
        <div class="home-shelf-title-block"><h2>${escapeHtml(opt.label)}</h2><p class="sub">${escapeHtml(opt.description)}</p></div>
        ${hasPages ? `<div class="home-shelf-controls" aria-label="${escapeForAttribute(opt.label)} shelf navigation"><span>${escapeHtml(rangeText)}</span><button class="home-shelf-arrow" data-home-shelf-nav="prev" data-home-shelf-id="${escapeForAttribute(id)}" type="button" ${offset <= 0 ? 'disabled' : ''} aria-label="Previous ${escapeForAttribute(opt.label)} items">‹</button><button class="home-shelf-arrow" data-home-shelf-nav="next" data-home-shelf-id="${escapeForAttribute(id)}" type="button" ${offset >= maxOffset ? 'disabled' : ''} aria-label="Next ${escapeForAttribute(opt.label)} items">›</button></div>` : ''}
      </div>
      <div class="card-row home-shelf-row${slideClass}" data-home-shelf-row="${escapeForAttribute(id)}">${visible.length ? visible.map(item => cardMarkupForItem(item)).join('') : `<div class="empty-message compact">No items for this shelf yet.</div>`}</div>
    </section>`;
  }).join('');
  initializeCoverImages(host);
  attachCoverPrimeScrollHandler();
  host.querySelectorAll('[data-home-shelf-nav]').forEach(btn => btn.addEventListener('click', handleHomeShelfNavigation));
  if (state.homeShelfSlideDirection && Object.keys(state.homeShelfSlideDirection).length) {
    window.setTimeout(() => { state.homeShelfSlideDirection = {}; }, 260);
  }
}
function handleHomeShelfNavigation(event) {
  event.preventDefault();
  event.stopPropagation();
  const btn = event.currentTarget;
  const id = btn?.dataset?.homeShelfId || '';
  const direction = btn?.dataset?.homeShelfNav === 'prev' ? -1 : 1;
  if (!id || state.homeShelfAnimating) return;
  const items = shelfItemsFor(id, state.items);
  const maxOffset = Math.max(0, items.length - HOME_SHELF_PAGE_SIZE);
  const current = Number((state.homeShelfOffsets || {})[id] || 0);
  const next = Math.min(Math.max(0, current + direction * HOME_SHELF_PAGE_SIZE), maxOffset);
  if (next === current) return;
  const row = btn.closest('.home-shelf')?.querySelector(`[data-home-shelf-row="${CSS.escape(id)}"]`);
  state.homeShelfAnimating = true;
  if (row) row.classList.add(direction > 0 ? 'slide-out-to-left' : 'slide-out-to-right');
  btn.closest('.home-shelf')?.querySelectorAll('[data-home-shelf-nav]').forEach(nav => { nav.disabled = true; });
  window.setTimeout(() => {
    state.homeShelfOffsets = { ...(state.homeShelfOffsets || {}), [id]: next };
    state.homeShelfSlideDirection = { ...(state.homeShelfSlideDirection || {}), [id]: direction };
    renderHomeShelves();
    window.setTimeout(() => { state.homeShelfAnimating = false; }, 120);
  }, row ? 145 : 0);
}
function cardMarkupForItem(item) {
  const cover = coverUrl(item);
  const itemId = String(item.id || item.Id || '');
  const favorite = isFavoriteItem(item);
  return `<article class="card ${specialCardClass(item)} ${state.selected?.id === item.id ? 'selected' : ''}" data-id="${escapeForAttribute(itemId)}" data-alpha="${alphaKey(displayTitle(item))}">
      <button class="favorite${favorite ? ' active' : ''}" type="button" data-id="${escapeForAttribute(itemId)}" aria-label="${favorite ? 'Remove from favorites' : 'Add to favorites'}" aria-pressed="${favorite ? 'true' : 'false'}" title="${favorite ? 'Remove from favorites' : 'Add to favorites'}">★</button>
      <div class="cover-wrap"><img decoding="async" loading="lazy" data-cover-src="${cover}" src="/assets/missing-cover.svg" alt="${escapeForAttribute(displayTitle(item))} cover" /></div>
      <div class="card-body">
        <div class="card-title">${escapeHtml(displayTitle(item))}</div>
        ${libraryCardPlatformMetaHtml(item)}
        <small>${escapeHtml(item.year)}</small>
        <div class="badge-line"><span class="format ${kindClass(item.kind)}">${escapeHtml(item.kind)}</span><span class="pill">${itemPageCountLabel(item)}</span></div>
      </div>
    </article>`;
}
function dateValue(value) {
  const t = Date.parse(value || '');
  return Number.isFinite(t) ? t : 0;
}
function normalizeCoverScale(value) {
  const n = Number(value);
  if (!Number.isFinite(n)) return 100;
  return Math.min(140, Math.max(75, Math.round(n / 5) * 5));
}
function applyLibraryCoverScale(value) {
  state.coverScale = normalizeCoverScale(value);
  document.documentElement.style.setProperty('--gv-cover-scale', (state.coverScale / 100).toFixed(2));
  const slider = $('coverSizeSlider');
  const label = $('coverSizeValue');
  if (slider) slider.value = String(state.coverScale);
  if (label) label.textContent = `${state.coverScale}%`;
}
function loadLibraryCoverScale() {
  let stored = 100;
  try { stored = Number(localStorage.getItem(GUIDEVAULT_COVER_SIZE_KEY) || '100'); } catch {}
  applyLibraryCoverScale(stored);
}
function setLibraryCoverScale(value) {
  applyLibraryCoverScale(value);
  try { localStorage.setItem(GUIDEVAULT_COVER_SIZE_KEY, String(state.coverScale)); } catch {}
}
function fileExtensionOf(item) {
  const candidates = [item?.fileName, item?.filename, item?.path, item?.relativePath, item?.title].map(v => String(v || '')).filter(Boolean);
  const value = candidates.find(v => /\.[A-Za-z0-9]{2,5}$/.test(v)) || '';
  const m = value.match(/\.([A-Za-z0-9]{2,5})$/);
  return m ? `.${m[1].toLowerCase()}` : String(item?.format || item?.Format || 'archive').toLowerCase();
}


function showDetailScreen(item) {
  if (!item) return;
  cleanupInactiveViewsForNavigation('detail');
  document.body.classList.remove('settings-sidebar-mode', 'reader-page-mode', 'profile-page-mode');
  document.body.classList.add('detail-page-mode');
  document.body.classList.remove('right-collapsed');
  state.selected = item;
  recordReadingActivity(item, 'view');
  hideAppView('libraryView');
  hideAppView('settingsView');
  hideAppView('profileView');
  hideAppView('readerView');
  showAppView('detailView');
  activateTab('overview');
  renderDetails(item);
  const main = document.querySelector('.main');
  if (main) main.scrollTop = 0;
}

function selectItem(item, rerender = true) {
  showDetailScreen(item);
}

function installLibraryCardDelegates() {
  ['grid', 'recentGrid'].forEach(id => {
    const host = $(id);
    if (!host || host.dataset.detailDelegateInstalled === 'true') return;
    host.dataset.detailDelegateInstalled = 'true';
    host.addEventListener('click', e => {
      if (e.target.closest?.('.favorite')) return;
      const card = e.target.closest?.('.card');
      if (!card || !host.contains(card)) return;
      const item = state.items.find(i => String(i.id) === String(card.dataset.id));
      if (item) {
        e.preventDefault();
        showDetailScreen(item);
      }
    });
  });
}

function installGlobalDetailDelegate() {
  if (document.body.dataset.guidevaultGlobalDetailDelegate === 'true') return;
  document.body.dataset.guidevaultGlobalDetailDelegate = 'true';
  document.addEventListener('click', e => {
    if (e.target.closest?.('.favorite')) return;
    const card = e.target.closest?.('article.card[data-id]');
    if (!card) return;
    if ($('settingsView')?.contains(card) || $('readerView')?.contains(card)) return;
    const item = state.items.find(i => String(i.id) === String(card.dataset.id));
    if (!item) return;
    e.preventDefault();
    e.stopPropagation();
    showDetailScreen(item);
  }, true);
}




function isBlankish(value) {
  const v = String(value || '').trim();
  return !v || v === '—' || /^unknown$/i.test(v) || /^unsorted$/i.test(v);
}
function detectedSystemOf(item) {
  if (!item) return '—';
  const kind = item.kind || '';
  const platforms = associatedPlatformsOf(item);
  const preferred = [item.system, item.category, item.primarySystem]
    .map(v => String(v || '').trim())
    .find(v => v && !isBlankish(v) && !isMultiPlatformBucketName(v));
  if (preferred) return preferred;
  if (kind === 'Strategy Guide') {
    if (platforms.length === 1) return platforms[0];
    if (platforms.length > 1) return MULTI_PLATFORM_LABEL;
    return 'Unsorted Strategy Guides';
  }
  if (!isBlankish(item.series)) return item.series;
  return '—';
}

function platformListHtml(item) {
  const platforms = associatedPlatformsOf(item);
  if (!platforms.length) return '—';
  return `<div class="platform-list">${platforms.map(p => `<span class="tag">${platformIconHtml(p, 'platform-icon tiny')}${escapeHtml(p)}</span>`).join('')}</div>`;
}

function normalizeWebLink(value) {
  const raw = String(value || '').trim();
  if (!raw) return '';
  if (/^https?:\/\//i.test(raw)) return raw;
  if (/^www\./i.test(raw)) return `https://${raw}`;
  return '';
}

function metaRow(label, value, isHtml = false) {
  const rendered = isHtml ? String(value || '') : escapeHtml(String(value || '—'));
  return `<dt>${escapeHtml(label)}</dt><dd>${rendered}</dd>`;
}

function itemArray(value) {
  if (Array.isArray(value)) return value.map(v => String(v || '').trim()).filter(Boolean);
  if (typeof value === 'string') return value.split(/[;,|]/).map(v => v.trim()).filter(Boolean);
  return [];
}

function itemList(value) {
  const values = itemArray(value);
  return values.length ? values.join(', ') : '—';
}

function optionValueOf(option) {
  return String(option?.value || option?.textContent || '').trim();
}

function setMultiSelectValues(id, values) {
  const select = $(id);
  if (!select) return;
  const selectedValues = itemArray(values);
  const selected = new Set(selectedValues.map(v => v.toLowerCase()));
  [...select.options].forEach(option => {
    option.selected = selected.has(optionValueOf(option).toLowerCase());
  });
  if (id === 'editGuideType' && !selected.size && select.options.length) {
    const fallback = [...select.options].find(option => optionValueOf(option).toLowerCase() === 'strategy guide');
    if (fallback) fallback.selected = true;
  }
  syncMetadataMultiSelectControl(id);
}

function getMultiSelectValues(id) {
  const select = $(id);
  if (!select) return [];
  return [...select.selectedOptions].map(option => optionValueOf(option)).filter(Boolean);
}

function metadataMultiSelectLabel(id, values = getMultiSelectValues(id)) {
  if (!values.length) {
    if (id === 'editEditionType') return 'Standard / Unspecified';
    return 'Select one or more';
  }
  if (values.length <= 2) return values.join(', ');
  return `${values.slice(0, 2).join(', ')} +${values.length - 2}`;
}

function syncMetadataMultiSelectControl(id) {
  const select = $(id);
  const shell = document.querySelector(`[data-multi-select="${id}"]`);
  if (!select || !shell) return;
  const values = getMultiSelectValues(id);
  const text = shell.querySelector('.meta-multi-value');
  if (text) text.textContent = metadataMultiSelectLabel(id, values);
  shell.querySelectorAll('input[type="checkbox"][data-value]').forEach(input => {
    const value = String(input.dataset.value || '').trim().toLowerCase();
    input.checked = values.some(v => String(v || '').trim().toLowerCase() === value);
  });
}

function closeMetadataMultiSelects(except = null) {
  document.querySelectorAll('.meta-multi-select.open').forEach(shell => {
    if (shell !== except) {
      shell.classList.remove('open');
      shell.querySelector('.meta-multi-button')?.setAttribute('aria-expanded', 'false');
    }
  });
}

function initMetadataMultiSelectControls() {
  document.querySelectorAll('.meta-multi-select[data-multi-select]').forEach(shell => {
    const id = shell.dataset.multiSelect || '';
    const select = $(id);
    if (!select) return;
    const panel = shell.querySelector('.meta-multi-panel');
    if (!panel || panel.dataset.bound === 'true') {
      syncMetadataMultiSelectControl(id);
      return;
    }
    panel.innerHTML = [...select.options].map(option => {
      const value = optionValueOf(option);
      return `<label class="meta-multi-option"><input class="meta-multi-option-input" type="checkbox" data-value="${escapeHtml(value)}" /> <span>${escapeHtml(value)}</span></label>`;
    }).join('');
    panel.dataset.bound = 'true';
    syncMetadataMultiSelectControl(id);
  });
}

function inferEditionTypes(value) {
  const text = String(value || '').trim();
  if (!text) return [];
  const types = [];
  if (/\d{4}\s+Edition/i.test(text)) types.push('Year Edition');
  if (/Volume\s+\d+/i.test(text)) types.push('Volume Edition');
  text.split(/[;,|]/).map(part => part.trim()).filter(Boolean).forEach(part => {
    if (/^\d{4}\s+Edition$/i.test(part) || /^Volume\s+\d+/i.test(part)) return;
    if (/^Standard\s*\/\s*Unspecified$/i.test(part)) return;
    if (!types.some(t => t.toLowerCase() === part.toLowerCase())) types.push(part);
  });
  return types;
}

function inferEditionType(value) {
  return inferEditionTypes(value).join(', ');
}

function inferEditionYear(value) {
  const match = String(value || '').match(/(\d{4})\s+Edition/i);
  return match ? match[1] : '';
}

function inferEditionVolume(value) {
  const match = String(value || '').match(/Volume\s+(\d+)/i);
  return match ? match[1] : '';
}

function buildEditionValue(types, year, volume) {
  const selected = itemArray(types);
  if (!selected.length) return '';
  return selected.map(type => {
    if (type === 'Year Edition') {
      const y = String(year || '').trim();
      return y ? `${y} Edition` : type;
    }
    if (type === 'Volume Edition') {
      const v = String(volume || '').trim();
      return v ? `Volume ${v}` : type;
    }
    return type;
  }).filter(Boolean).join(', ');
}

function updateEditionControls() {
  const kind = $('editKind')?.value || state.selected?.kind || '';
  const types = getMultiSelectValues('editEditionType');
  const showStrategy = kind === 'Strategy Guide';
  const yearLabel = $('editEditionYearLabel');
  const volumeLabel = $('editEditionVolumeLabel');
  if (yearLabel) yearLabel.classList.toggle('hidden', !(showStrategy && types.includes('Year Edition')));
  if (volumeLabel) volumeLabel.classList.toggle('hidden', !(showStrategy && types.includes('Volume Edition')));
}

function setMaybeValue(id, value) {
  const el = $(id);
  if (el) el.value = value || '';
}

function updateTypedMetadataFieldVisibility(kind = $('editKind')?.value || '') {
  const isMagazine = kind === 'Magazine';
  const isStrategyGuide = kind === 'Strategy Guide';
  const isManual = kind === 'Manual';
  document.querySelectorAll('.magazine-metadata-field, .strategy-metadata-field, .manual-metadata-field').forEach(el => {
    const show = (isMagazine && el.classList.contains('magazine-metadata-field'))
      || (isStrategyGuide && el.classList.contains('strategy-metadata-field'))
      || (isManual && el.classList.contains('manual-metadata-field'));
    el.classList.toggle('hidden', !show);
  });
  if ($('editIssueLabel')) $('editIssueLabel').classList.toggle('hidden', !isMagazine);
  if ($('editEsrbRatingLabel')) $('editEsrbRatingLabel').classList.toggle('hidden', isMagazine);
  updateEditionControls();
}

function setMagazineFieldsVisible(visible) {
  const kind = visible ? 'Magazine' : ($('editKind')?.value || '');
  updateTypedMetadataFieldVisibility(kind);
}

function setStrategyGuideFieldsVisible(visible) {
  const kind = visible ? 'Strategy Guide' : ($('editKind')?.value || '');
  updateTypedMetadataFieldVisibility(kind);
}

function csvInput(id) {
  const el = $(id);
  return el ? el.value.split(/[;,|]/).map(v => v.trim()).filter(Boolean) : [];
}

function metadataOverrideKey(itemOrId) {
  const id = typeof itemOrId === 'string' ? itemOrId : (itemOrId?.id || itemOrId?.Id || '');
  return String(id || '').trim();
}

function readClientMetadataOverrides() {
  try {
    return JSON.parse(localStorage.getItem(GUIDEVAULT_METADATA_OVERRIDES_KEY) || '{}') || {};
  } catch {
    return {};
  }
}

function writeClientMetadataOverrides(map) {
  try { localStorage.setItem(GUIDEVAULT_METADATA_OVERRIDES_KEY, JSON.stringify(map || {})); } catch {}
}

function normalizeClientMetadataPayload(payload) {
  const clone = { ...(payload || {}) };
  ['tags','associatedPlatforms','featuredGames','featuredPlatforms','specialFeatures','includedExtras','coveredGames','coveredPlatforms','guideTopics','charactersCovered','locationsCovered','includedSections','itemsCovered'].forEach(key => {
    if (clone[key] !== undefined && !Array.isArray(clone[key])) clone[key] = itemArray(clone[key]);
  });
  return clone;
}

function rememberClientMetadataOverride(itemOrId, payload) {
  const key = metadataOverrideKey(itemOrId);
  if (!key || !payload) return;
  const map = readClientMetadataOverrides();
  map[key] = { ...(map[key] || {}), ...normalizeClientMetadataPayload(payload), savedAt: new Date().toISOString() };
  writeClientMetadataOverrides(map);
}

function applyClientMetadataOverride(item) {
  if (!item) return item;
  const key = metadataOverrideKey(item);
  if (!key) return item;
  const map = readClientMetadataOverrides();
  const override = map[key];
  if (!override) return item;
  const { savedAt, ...metadata } = override;
  return mergeSavedMetadataClientSide(item, {}, metadata);
}

function applyClientMetadataOverridesToLibrary() {
  if (!Array.isArray(state.items) || !state.items.length) return;
  state.items = state.items.map(item => applyClientMetadataOverride(item));
  if (state.selected) state.selected = applyClientMetadataOverride(state.selected);
}


const METADATA_MANAGER_DEFAULT_COLUMNS = ['kind','name','category','associatedPlatforms','series','languageTag','region','year','publisher','topics','metadataSource'];
const METADATA_MANAGER_ARRAY_FIELDS = new Set([
  'tags','associatedPlatforms','featuredGames','featuredPlatforms','specialFeatures','includedExtras',
  'coveredGames','coveredPlatforms','guideTopics','charactersCovered','locationsCovered','includedSections','itemsCovered'
]);
const METADATA_MANAGER_READONLY_COLUMNS = new Set(['kind','metadataSource']);
const METADATA_MANAGER_WIDE_COLUMNS = new Set(['name','category','series','publisher','topics','summary','notes','webLink','associatedPlatforms','featuredGames','featuredPlatforms','specialFeatures','includedExtras','coveredGames','coveredPlatforms','guideTopics','charactersCovered','locationsCovered','includedSections','itemsCovered','platformMatchTitle','platformResolverSource','coverSubject']);
const METADATA_MANAGER_ALL_COLUMNS = [
  { key:'kind', label:'Type', description:'Manual, Strategy Guide, or Magazine.' },
  { key:'name', label:'Title / Name', description:'Guidevault display title for the item.' },
  { key:'category', label:'Preferred Platform', description:'Primary/preferred platform used when no associated-platform override is present.' },
  { key:'series', label:'Series / Publication', description:'Series, franchise, or magazine publication name.' },
  { key:'languageTag', label:'Language', description:'Full language value, such as English.' },
  { key:'region', label:'Region', description:'Region code such as US, JP, or EU.' },
  { key:'year', label:'Year', description:'Release or publication year.' },
  { key:'publisher', label:'Publisher', description:'Publisher or game publisher.' },
  { key:'topics', label:'Topics / Games', description:'Main topic list used by the entry type.' },
  { key:'metadataSource', label:'Source', description:'Where the current metadata came from.' },
  { key:'summary', label:'Summary', description:'Description or overview text.' },
  { key:'writer', label:'Writer', description:'Writer/author field.' },
  { key:'rating', label:'Rating', description:'Rating or ESRB-style metadata.' },
  { key:'webLink', label:'Web Link', description:'Reference URL.' },
  { key:'asin', label:'ASIN', description:'Amazon ASIN/book identifier.' },
  { key:'isbn10', label:'ISBN-10', description:'ISBN-10 book identifier.' },
  { key:'isbn13', label:'ISBN-13', description:'ISBN-13 book identifier.' },
  { key:'associatedPlatforms', label:'Associated Platforms', description:'Platforms where this item should appear in the library. Strategy guides can appear under each listed platform.' },
  { key:'platformMatchTitle', label:'Platform Match Title', description:'Title used during platform matching.' },
  { key:'platformResolverSource', label:'Platform Resolver Source', description:'Where platform resolution came from.' },
  { key:'magazineTitle', label:'Magazine Title', description:'Magazine publication title.' },
  { key:'issueNumber', label:'Issue Number', description:'Magazine issue number.' },
  { key:'volume', label:'Volume', description:'Magazine volume value.' },
  { key:'coverDate', label:'Cover Date', description:'Cover date printed on the issue.' },
  { key:'publicationDate', label:'Publication Date', description:'Publication/release date.' },
  { key:'platformFocus', label:'Platform Focus', description:'Magazine platform focus.' },
  { key:'primarySystem', label:'Primary System', description:'Primary platform/system for magazines.' },
  { key:'magazineCategory', label:'Magazine Category', description:'Magazine category/classification.' },
  { key:'coverSubject', label:'Cover Subject', description:'Main cover subject or feature.' },
  { key:'featuredGames', label:'Featured Games', description:'Magazine featured games.' },
  { key:'featuredPlatforms', label:'Featured Platforms', description:'Magazine featured platforms.' },
  { key:'specialFeatures', label:'Special Features', description:'Magazine special features.' },
  { key:'includedExtras', label:'Included Extras', description:'Included extras such as posters/discs.' },
  { key:'gameTitle', label:'Game Title', description:'Game title for manuals/guides.' },
  { key:'guideType', label:'Guide Type', description:'Strategy guide type.' },
  { key:'edition', label:'Edition', description:'Edition or print identifier.' },
  { key:'franchise', label:'Franchise', description:'Franchise/series for manuals and guides.' },
  { key:'developer', label:'Developer', description:'Game developer.' },
  { key:'gamePublisher', label:'Game Publisher', description:'Game publisher.' },
  { key:'gameReleaseYear', label:'Game Release Year', description:'Game release year.' },
  { key:'genre', label:'Genre', description:'Game or book genre.' },
  { key:'coveredGames', label:'Covered Games', description:'Games covered by a strategy guide.' },
  { key:'coveredPlatforms', label:'Covered Platforms', description:'Platforms covered by a guide.' },
  { key:'guideTopics', label:'Guide Topics', description:'Strategy guide topic list.' },
  { key:'charactersCovered', label:'Characters Covered', description:'Characters covered.' },
  { key:'locationsCovered', label:'Locations Covered', description:'Locations/areas covered.' },
  { key:'manualTitle', label:'Manual Title', description:'Manual-specific title.' },
  { key:'manualType', label:'Manual Type', description:'Manual type/classification.' },
  { key:'includedSections', label:'Included Sections', description:'Manual sections included.' },
  { key:'controlScheme', label:'Control Scheme', description:'Controller/control information.' },
  { key:'itemsCovered', label:'Items Covered', description:'Items, equipment, or content covered.' },
  { key:'warrantySupport', label:'Warranty / Support', description:'Warranty/support notes.' },
  { key:'notes', label:'Notes', description:'Freeform notes.' }
];

function metadataManagerItemId(item) {
  return String(item?.id || item?.Id || '').trim();
}

function metadataManagerTopicField(item) {
  const kind = String(item?.kind || '').toLowerCase();
  if (kind === 'magazine') return 'featuredGames';
  if (kind === 'strategy guide') return 'guideTopics';
  if (kind === 'manual') return 'includedSections';
  return 'tags';
}

function metadataManagerTopicValue(item) {
  const field = metadataManagerTopicField(item);
  const values = itemArray(item?.[field]);
  if (values.length) return values.join(', ');
  return itemArray(item?.tags).join(', ');
}

function metadataManagerItemName(item) {
  if (!item) return '';
  if (item.kind === 'Magazine') {
    const base = item.magazineTitle || item.series || item.title || '';
    const issue = String(item.issueNumber || '').trim();
    return issue ? `${base} #${issue}` : base;
  }
  if (item.kind === 'Manual') return item.manualTitle || item.gameTitle || item.title || '';
  if (item.kind === 'Strategy Guide') return item.gameTitle || item.title || '';
  return item.title || '';
}

function metadataManagerCategoryValue(item) {
  return preferredPlatformOf(item);
}

function metadataManagerSeriesValue(item) {
  if (item?.kind === 'Magazine') return item.magazineTitle || item.series || '';
  if (item?.kind === 'Strategy Guide') return item.franchise || item.series || '';
  return item?.series || item?.franchise || '';
}

function metadataManagerIsMultiPlatformStrategyGuide(item) {
  if (!item || item.kind !== 'Strategy Guide') return false;
  const platforms = associatedPlatformsOf(item);
  if (platforms.length > 1) return true;
  const buckets = [item.system, item.category, item.primarySystem, metadataManagerCategoryValue(item)]
    .map(v => String(v || '').trim())
    .filter(Boolean);
  return buckets.some(v => /^multi[-\s]*platform strategy guides$/i.test(v));
}

function metadataManagerIsUnsortedStrategyGuide(item) {
  if (!item || item.kind !== 'Strategy Guide') return false;
  if (metadataManagerIsMultiPlatformStrategyGuide(item)) return false;
  const buckets = [item.system, item.category, item.primarySystem, metadataManagerCategoryValue(item)]
    .map(v => String(v || '').trim())
    .filter(Boolean);
  if (!buckets.length) return true;
  return buckets.some(v => /^unsorted(?: strategy guides)?$/i.test(v));
}

function metadataManagerEditableItems() {
  const allowed = new Set(['Manual', 'Strategy Guide', 'Magazine']);
  const manager = state.metadataManager || {};
  const q = String(manager.search || '').trim().toLowerCase();
  const kind = String(manager.filterKind || '').trim();
  const missing = String(manager.missing || '').trim();
  const category = String(manager.category || '').trim().toLowerCase();
  return (state.items || []).filter(item => {
    if (!allowed.has(item.kind)) return false;
    if (kind && item.kind !== kind) return false;
    if (category) {
      const cats = [metadataManagerCategoryValue(item), metadataManagerSeriesValue(item), item.magazineTitle, item.series, ...associatedPlatformsOf(item)]
        .map(v => String(v || '').trim().toLowerCase()).filter(Boolean);
      if (!cats.includes(category)) return false;
    }
    if (missing === 'missing-language' && String(item.languageTag || '').trim()) return false;
    if (missing === 'missing-region' && String(item.region || '').trim()) return false;
    if (missing === 'missing-publisher' && String(item.publisher || item.gamePublisher || '').trim()) return false;
    if (missing === 'missing-topics' && metadataManagerTopicValue(item).trim()) return false;
    if (missing === 'unsorted-strategy-guides' && !metadataManagerIsUnsortedStrategyGuide(item)) return false;
    if (missing === 'multi-platform-strategy-guides' && !metadataManagerIsMultiPlatformStrategyGuide(item)) return false;
    if (q) {
      const haystack = [metadataManagerItemName(item), item.title, item.kind, metadataManagerCategoryValue(item), metadataManagerSeriesValue(item), item.publisher, item.gamePublisher, item.year, item.languageTag, item.region, metadataManagerTopicValue(item), platformListText(item), item.metadataSource, item.summary, ...(item.tags || [])].join(' ').toLowerCase();
      if (!haystack.includes(q)) return false;
    }
    return true;
  }).sort((a, b) => metadataManagerItemName(a).localeCompare(metadataManagerItemName(b), undefined, { sensitivity: 'base' }));
}

function metadataManagerSetStatus(message = '', tone = '') {
  const el = $('metadataManagerStatus');
  if (!el) return;
  el.textContent = message;
  el.classList.toggle('success', tone === 'success');
  el.classList.toggle('error', tone === 'error');
}

function metadataManagerUniqueCategories(items) {
  const values = new Map();
  items.forEach(item => {
    [metadataManagerCategoryValue(item), metadataManagerSeriesValue(item), ...associatedPlatformsOf(item)].forEach(value => {
      const key = String(value || '').trim();
      if (key && !isMultiPlatformBucketName(key)) values.set(key.toLowerCase(), key);
    });
  });
  return Array.from(values.values()).sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }));
}

function metadataManagerRenderCategoryFilter() {
  const select = $('metadataManagerCategory');
  if (!select) return;
  const current = state.metadataManager.category || '';
  const options = metadataManagerUniqueCategories((state.items || []).filter(item => ['Manual', 'Strategy Guide', 'Magazine'].includes(item.kind)));
  select.innerHTML = `<option value="">All platforms and series</option>${options.map(v => `<option value="${escapeForAttribute(v)}">${escapeHtml(v)}</option>`).join('')}`;
  select.value = current;
}

function metadataManagerSummaryStats(items) {
  const total = items.length;
  const missingLanguage = items.filter(i => !String(i.languageTag || '').trim()).length;
  const missingRegion = items.filter(i => !String(i.region || '').trim()).length;
  const missingTopics = items.filter(i => !metadataManagerTopicValue(i).trim()).length;
  const unsortedGuides = items.filter(metadataManagerIsUnsortedStrategyGuide).length;
  const multiPlatformGuides = items.filter(metadataManagerIsMultiPlatformStrategyGuide).length;
  const edited = Object.keys(state.metadataManager.dirty || {}).length;
  return [
    ['Visible', total],
    ['Missing language', missingLanguage],
    ['Missing region', missingRegion],
    ['Missing topics', missingTopics],
    ['Unsorted guides', unsortedGuides],
    ['Multiple platforms', multiPlatformGuides],
    ['Edited rows', edited]
  ];
}

function metadataManagerFieldValue(item, field) {
  if (field === 'name') return metadataManagerItemName(item);
  if (field === 'category') return metadataManagerCategoryValue(item);
  if (field === 'series') return metadataManagerSeriesValue(item);
  if (field === 'publisher') return item.publisher || item.gamePublisher || '';
  if (field === 'topics') return metadataManagerTopicValue(item);
  const value = item?.[field];
  if (Array.isArray(value)) return value.join(', ');
  return value ?? '';
}


function metadataManagerCoverPreviewItem(id) {
  const lookup = String(id || '').trim();
  if (!lookup) return null;
  return (state.items || []).find(item => metadataManagerItemId(item) === lookup) || null;
}

function metadataManagerEnsureCoverPreview() {
  let preview = $('metadataManagerCoverPreview');
  if (preview) return preview;
  preview = document.createElement('div');
  preview.id = 'metadataManagerCoverPreview';
  preview.className = 'metadata-cover-preview hidden';
  preview.setAttribute('aria-hidden', 'true');
  document.body.appendChild(preview);
  return preview;
}

function metadataManagerPositionCoverPreview(anchor, preview) {
  if (!anchor || !preview) return;
  const rect = anchor.getBoundingClientRect();
  const width = Math.min(220, Math.max(170, preview.offsetWidth || 190));
  const height = Math.min(360, Math.max(260, preview.offsetHeight || 300));
  const margin = 14;
  let left = rect.left;
  let top = rect.bottom + 10;
  if (left + width + margin > window.innerWidth) left = window.innerWidth - width - margin;
  if (left < margin) left = margin;
  if (top + height + margin > window.innerHeight) top = rect.top - height - 10;
  if (top < margin) top = Math.max(margin, rect.bottom + 10);
  preview.style.left = `${Math.round(left)}px`;
  preview.style.top = `${Math.round(top)}px`;
}

function metadataManagerShowCoverPreview(id, anchor) {
  const item = metadataManagerCoverPreviewItem(id);
  if (!item) return;
  const preview = metadataManagerEnsureCoverPreview();
  const title = metadataManagerItemName(item) || item.title || 'Untitled';
  const category = metadataManagerCategoryValue(item) || metadataManagerSeriesValue(item) || 'Unsorted';
  const year = String(item.year || item.coverDate || item.publicationDate || '').trim();
  preview.innerHTML = `
    <div class="metadata-cover-preview-art">
      <img src="${coverUrl(item)}" alt="${escapeForAttribute(title)} cover" onerror="this.onerror=null;this.src='/assets/missing-cover.svg';" />
    </div>
    <div class="metadata-cover-preview-caption">
      <strong>${escapeHtml(title)}</strong>
      <span>${escapeHtml(item.kind || 'Entry')}${year ? ` • ${escapeHtml(year)}` : ''}</span>
      <em>${escapeHtml(category)}</em>
    </div>
  `;
  preview.classList.remove('hidden');
  preview.setAttribute('aria-hidden', 'false');
  metadataManagerPositionCoverPreview(anchor, preview);
}

function metadataManagerHideCoverPreview() {
  const preview = $('metadataManagerCoverPreview');
  if (!preview) return;
  preview.classList.add('hidden');
  preview.setAttribute('aria-hidden', 'true');
}

function metadataManagerClearCoverPreviewTimer() {
  if (state.metadataManager?.previewTimer) {
    clearTimeout(state.metadataManager.previewTimer);
    state.metadataManager.previewTimer = null;
  }
}

function metadataManagerHandlePreviewPointerDown(event) {
  const pill = event.target.closest?.('[data-metadata-preview-id]');
  if (!pill) return;
  if (event.pointerType === 'mouse' && event.button !== 0) return;
  metadataManagerClearCoverPreviewTimer();
  const id = pill.dataset.metadataPreviewId || '';
  state.metadataManager = state.metadataManager || {};
  state.metadataManager.previewTimer = setTimeout(() => {
    state.metadataManager.previewTimer = null;
    metadataManagerShowCoverPreview(id, pill);
  }, 280);
}

function metadataManagerHandlePreviewPointerEnd() {
  metadataManagerClearCoverPreviewTimer();
  metadataManagerHideCoverPreview();
}

function metadataManagerRowPayload(item, changes = {}) {
  const payload = { metadataSource: 'Bulk metadata manager' };
  const allowedKeys = new Set(METADATA_MANAGER_ALL_COLUMNS.map(column => column.key));
  Object.entries(changes || {}).forEach(([field, value]) => {
    if (METADATA_MANAGER_READONLY_COLUMNS.has(field)) return;
    if (field === 'name') {
      payload.title = value;
      if (item.kind === 'Manual') payload.manualTitle = value;
      if (item.kind === 'Strategy Guide') payload.gameTitle = value;
      if (item.kind === 'Magazine') payload.magazineTitle = value;
    } else if (field === 'category') {
      payload.category = value;
      payload.system = value;
      if (item.kind === 'Magazine') payload.primarySystem = value;
    } else if (field === 'series') {
      payload.series = value;
      if (item.kind === 'Magazine') payload.magazineTitle = value;
      if (item.kind === 'Strategy Guide' || item.kind === 'Manual') payload.franchise = value;
    } else if (field === 'publisher') {
      payload.publisher = value;
      if (item.kind === 'Strategy Guide' || item.kind === 'Manual') payload.gamePublisher = value;
    } else if (field === 'topics') {
      payload[metadataManagerTopicField(item)] = itemArray(value);
    } else if (METADATA_MANAGER_ARRAY_FIELDS.has(field)) {
      payload[field] = itemArray(value);
    } else if (allowedKeys.has(field)) {
      payload[field] = String(value ?? '');
    }
  });
  if (item.kind === 'Strategy Guide') {
    const platformSource = changes.associatedPlatforms !== undefined ? itemArray(changes.associatedPlatforms) : associatedPlatformsOf(item);
    if (hasMultipleAssociatedPlatforms(platformSource)) {
      payload.category = MULTI_PLATFORM_LABEL;
      payload.system = MULTI_PLATFORM_LABEL;
    }
  }
  return payload;
}

function metadataManagerUpdateItemLocal(id, payload) {
  const index = (state.items || []).findIndex(item => metadataManagerItemId(item) === id);
  if (index < 0) return null;
  const updated = mergeSavedMetadataClientSide(state.items[index], {}, payload);
  updated.id = updated.id || id;
  updated.Id = updated.Id || id;
  state.items[index] = updated;
  rememberClientMetadataOverride(id, payload);
  if (state.selected && metadataManagerItemId(state.selected) === id) state.selected = updated;
  return updated;
}


function metadataManagerValidColumnKeys() {
  return new Set(METADATA_MANAGER_ALL_COLUMNS.map(column => column.key));
}

function metadataManagerLoadVisibleColumns() {
  const valid = metadataManagerValidColumnKeys();
  let saved = [];
  try { saved = JSON.parse(localStorage.getItem(GUIDEVAULT_METADATA_COLUMNS_KEY) || '[]') || []; } catch { saved = []; }
  const columns = (Array.isArray(state.metadataManager?.visibleColumns) && state.metadataManager.visibleColumns.length ? state.metadataManager.visibleColumns : saved)
    .filter(key => valid.has(key));
  const resolved = columns.length ? columns : METADATA_MANAGER_DEFAULT_COLUMNS.slice();
  state.metadataManager.visibleColumns = resolved;
  return resolved;
}

function metadataManagerSaveVisibleColumns(columns) {
  const valid = metadataManagerValidColumnKeys();
  const resolved = (columns || []).filter(key => valid.has(key));
  const safe = resolved.length ? resolved : METADATA_MANAGER_DEFAULT_COLUMNS.slice();
  state.metadataManager.visibleColumns = safe;
  try { localStorage.setItem(GUIDEVAULT_METADATA_COLUMNS_KEY, JSON.stringify(safe)); } catch {}
  return safe;
}

function metadataManagerVisibleColumns() {
  const visible = new Set(metadataManagerLoadVisibleColumns());
  return METADATA_MANAGER_ALL_COLUMNS.filter(column => visible.has(column.key));
}

function metadataManagerSetColumnVisible(key, visible) {
  const current = metadataManagerLoadVisibleColumns();
  const set = new Set(current);
  if (visible) set.add(key); else set.delete(key);
  metadataManagerSaveVisibleColumns(Array.from(set));
  renderMetadataManager();
}

function metadataManagerResetColumns() {
  metadataManagerSaveVisibleColumns(METADATA_MANAGER_DEFAULT_COLUMNS.slice());
  renderMetadataManager();
  metadataManagerSetStatus('Metadata columns reset to the default view.', 'success');
}

function metadataManagerShowAllColumns() {
  metadataManagerSaveVisibleColumns(METADATA_MANAGER_ALL_COLUMNS.map(column => column.key));
  renderMetadataManager();
  metadataManagerSetStatus('All available metadata columns are visible.', 'success');
}

function renderMetadataManagerColumnPicker() {
  const picker = $('metadataManagerColumnPicker');
  if (!picker) return;
  const visible = new Set(metadataManagerLoadVisibleColumns());
  picker.innerHTML = METADATA_MANAGER_ALL_COLUMNS.map(column => `
    <label class="metadata-manager-column-option" title="${escapeForAttribute(column.description || '')}">
      <input type="checkbox" data-column-key="${escapeForAttribute(column.key)}" ${visible.has(column.key) ? 'checked' : ''} />
      <span>${escapeHtml(column.label)}</span>
    </label>
  `).join('');
  const active = $('metadataManagerColumnActiveCount');
  if (active) active.textContent = `${visible.size} of ${METADATA_MANAGER_ALL_COLUMNS.length} columns shown`;
}

function metadataManagerSourceTitleCandidate(item) {
  const candidates = [
    item?.manualTitle, item?.gameTitle, item?.magazineTitle, item?.title, item?.name,
    item?.fileName, item?.filename, item?.sourceFile, item?.sourcePath, item?.path, item?.filePath, item?.libraryPath
  ];
  const found = candidates.find(value => String(value || '').trim());
  return String(found || metadataManagerItemName(item) || '').trim();
}

function metadataManagerTitleCase(text) {
  const preserve = new Set(['NES','SNES','N64','GBA','GBC','GB','DS','3DS','Wii','WiiU','PS1','PS2','PS3','PS4','PS5','PSP','PSVita','PC','DOS','CD','DVD','HD','VR','III','IV','VI','VII','VIII','IX','XI','XII','XIII','XIV','XV','USA','US','UK','EU','JP']);
  return String(text || '').split(/(\s+|-|:)/).map(part => {
    if (!part.trim() || /^\s+$|^-$|^:$/.test(part)) return part;
    const upper = part.toUpperCase();
    if (preserve.has(upper)) return upper;
    if (/^[IVXLCDM]+$/i.test(part) && part.length <= 6) return upper;
    if (/^[A-Z0-9]{2,}$/.test(part)) return part;
    return part.charAt(0).toUpperCase() + part.slice(1).toLowerCase();
  }).join('');
}

function metadataManagerCleanTitle(value, kind = '') {
  let text = String(value || '').trim();
  if (!text) return '';
  text = text.replace(/^.*[\\/]/, '');
  try { text = decodeURIComponent(text); } catch {}
  text = text.replace(/\.(cbz|cbr|pdf|zip|rar|7z)$/i, '');
  text = text.replace(/[._]+/g, ' ');
  text = text.replace(/[\[\(]\s*(usa|us|u|japan|jp|europe|eu|world|english|eng|en|scan|scanned|retromags|no-intro|goodtools|rev\s*\d+|v\d+(?:\.\d+)*)\s*[\]\)]/gi, ' ');
  text = text.replace(/\b(retromags|scan|scanned|cbz|cbr|pdf)\b/gi, ' ');
  if (/manual/i.test(kind)) text = text.replace(/\b(instruction\s+booklet|instruction\s+manual|manual)\b/gi, ' ');
  text = text.replace(/\s+#\s*/g, ' #');
  text = text.replace(/\s*[-–—]\s*/g, ' - ');
  text = text.replace(/\s*:\s*/g, ': ');
  text = text.replace(/\s{2,}/g, ' ').trim(' ', '-', '_', '.');
  return metadataManagerTitleCase(text);
}

function metadataManagerNormalizedIdentityPayload(item) {
  const payload = {};
  const cleanedName = metadataManagerCleanTitle(metadataManagerSourceTitleCandidate(item), item?.kind || '');
  if (!cleanedName) return payload;
  if (item.kind === 'Magazine') {
    const magazineBase = metadataManagerCleanTitle(item.magazineTitle || item.series || cleanedName.replace(/\s+#\s*\S+\s*$/i, ''), item.kind);
    const issue = String(item.issueNumber || '').trim() || (String(cleanedName).match(/#\s*([0-9A-Za-z.-]+)/)?.[1] || '');
    const display = issue ? `${magazineBase} #${issue}` : magazineBase;
    payload.title = display;
    payload.magazineTitle = magazineBase || display;
    if (!String(item.series || '').trim()) payload.series = magazineBase || display;
    if (issue && !String(item.issueNumber || '').trim()) payload.issueNumber = issue;
    return payload;
  }
  payload.title = cleanedName;
  if (item.kind === 'Manual') {
    payload.manualTitle = cleanedName;
    if (!String(item.gameTitle || '').trim()) payload.gameTitle = cleanedName;
  }
  if (item.kind === 'Strategy Guide') {
    payload.gameTitle = cleanedName;
  }
  return payload;
}

function metadataManagerNormalizedPayload(item) {
  const topicField = metadataManagerTopicField(item);
  const payload = {
    metadataSource: 'Bulk normalize',
    ...metadataManagerNormalizedIdentityPayload(item),
    languageTag: String(item.languageTag || '').trim() || 'English',
    region: String(item.region || '').trim() || 'US',
    tags: mergeTokenLists(item.tags || [], [], 'add')
  };
  if (item[topicField] !== undefined) payload[topicField] = mergeTokenLists(item[topicField], [], 'add');
  if (item.kind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(item)) {
    payload.category = MULTI_PLATFORM_LABEL;
    payload.system = MULTI_PLATFORM_LABEL;
  } else {
    if (!String(metadataManagerCategoryValue(item) || '').trim() && item.system) payload.category = item.system;
    if (!String(payload.category || '').trim() && String(item.primarySystem || '').trim()) payload.category = item.primarySystem;
  }
  return payload;
}

function renderMetadataManager() {
  if (!$('settingsMetadataManagerPanel')) return;
  state.metadataManager = state.metadataManager || { selectedIds: [], dirty: {}, filterKind: '', search: '', missing: '', category: '', visibleColumns: [] };
  metadataManagerRenderCategoryFilter();
  renderMetadataManagerColumnPicker();
  const manager = state.metadataManager;
  if ($('metadataManagerSearch')) $('metadataManagerSearch').value = manager.search || '';
  if ($('metadataManagerKind')) $('metadataManagerKind').value = manager.filterKind || '';
  if ($('metadataManagerMissing')) $('metadataManagerMissing').value = manager.missing || '';
  if ($('metadataManagerCategory')) $('metadataManagerCategory').value = manager.category || '';
  const items = metadataManagerEditableItems();
  const selectedSet = new Set(manager.selectedIds || []);
  const dirty = manager.dirty || {};
  const columns = metadataManagerVisibleColumns();
  const summary = $('metadataManagerSummary');
  if (summary) {
    summary.innerHTML = metadataManagerSummaryStats(items).map(([label, value]) => `<div class="metadata-manager-stat"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></div>`).join('');
  }
  const headerRow = $('metadataManagerHeaderRow');
  if (headerRow) {
    headerRow.innerHTML = `
      <th><input id="metadataManagerHeaderCheck" type="checkbox" aria-label="Select visible rows" /></th>
      ${columns.map(column => `<th>${escapeHtml(column.label)}</th>`).join('')}
    `;
  }
  const body = $('metadataManagerTableBody');
  if (body) {
    body.innerHTML = items.length ? items.map(item => {
      const id = metadataManagerItemId(item);
      const rowDirty = dirty[id] || {};
      const rowClass = Object.keys(rowDirty).length ? ' class="metadata-row-dirty"' : '';
      const rowAssociatedPlatforms = rowDirty.associatedPlatforms !== undefined ? itemArray(rowDirty.associatedPlatforms) : associatedPlatformsOf(item);
      const preferredPlatformReadOnly = item.kind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(rowAssociatedPlatforms);
      const inputField = (name, value, readOnly = false, title = '') => `<input class="metadata-manager-input ${METADATA_MANAGER_WIDE_COLUMNS.has(name) ? 'wide' : ''} ${readOnly ? 'readonly' : ''}" data-id="${escapeForAttribute(id)}" data-field="${escapeForAttribute(name)}" value="${escapeForAttribute(rowDirty[name] ?? value ?? '')}" ${readOnly ? 'readonly aria-readonly="true"' : ''} ${title ? `title="${escapeForAttribute(title)}"` : ''} />`;
      const cellFor = column => {
        if (column.key === 'kind') return `<span class="metadata-kind-pill metadata-kind-preview-trigger" data-metadata-preview-id="${escapeForAttribute(metadataManagerItemId(item))}" title="Click and hold to preview cover">${escapeHtml(item.kind || '—')}</span>`;
        if (column.key === 'metadataSource') return `<span class="metadata-source-text">${escapeHtml(item.metadataSource || '—')}</span>`;
        if (column.key === 'category' && preferredPlatformReadOnly) return `<input class="metadata-manager-input ${METADATA_MANAGER_WIDE_COLUMNS.has('category') ? 'wide' : ''} readonly" data-id="${escapeForAttribute(id)}" data-field="category" value="${escapeForAttribute(MULTI_PLATFORM_LABEL)}" readonly aria-readonly="true" title="Preferred Platform is read-only when Associated Platforms contains multiple systems." />`;
        return inputField(column.key, metadataManagerFieldValue(item, column.key));
      };
      return `<tr${rowClass}>
        <td><input class="metadata-manager-row-check" type="checkbox" data-id="${escapeForAttribute(id)}" ${selectedSet.has(id) ? 'checked' : ''} /></td>
        ${columns.map(column => `<td>${cellFor(column)}</td>`).join('')}
      </tr>`;
    }).join('') : `<tr><td colspan="${columns.length + 1}" class="metadata-manager-empty">No metadata entries match this filter.</td></tr>`;
  }
  if ($('metadataManagerCount')) $('metadataManagerCount').textContent = `${items.length} visible / ${(state.items || []).filter(i => ['Manual','Strategy Guide','Magazine'].includes(i.kind)).length} editable entries`;
  if ($('metadataManagerDirtyCount')) $('metadataManagerDirtyCount').textContent = `${Object.keys(dirty).length} edited`;
  const headerCheck = $('metadataManagerHeaderCheck');
  if (headerCheck) {
    headerCheck.addEventListener('change', e => metadataManagerSelectVisible(!!e.currentTarget.checked));
    const visibleIds = items.map(metadataManagerItemId).filter(Boolean);
    headerCheck.checked = visibleIds.length > 0 && visibleIds.every(id => selectedSet.has(id));
  }
}

function metadataManagerUpdateFilter(field, value) {
  state.metadataManager = state.metadataManager || {};
  state.metadataManager[field] = value;
  renderMetadataManager();
}

function metadataManagerToggleSelection(id, checked) {
  if (!id) return;
  const selected = new Set(state.metadataManager.selectedIds || []);
  if (checked) selected.add(id); else selected.delete(id);
  state.metadataManager.selectedIds = Array.from(selected);
  renderMetadataManager();
}

function metadataManagerSelectVisible(select = true) {
  const visibleIds = metadataManagerEditableItems().map(metadataManagerItemId).filter(Boolean);
  const selected = new Set(state.metadataManager.selectedIds || []);
  visibleIds.forEach(id => select ? selected.add(id) : selected.delete(id));
  state.metadataManager.selectedIds = Array.from(selected);
  renderMetadataManager();
}

function metadataManagerMarkDirty(id, field, value) {
  if (!id || !field) return;
  const item = (state.items || []).find(i => metadataManagerItemId(i) === id);
  if (!item) return;
  if (field === 'category' && item.kind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(item)) return;
  state.metadataManager.dirty = state.metadataManager.dirty || {};
  const current = String(metadataManagerFieldValue(item, field) ?? '');
  if (String(value ?? '') === current) {
    if (state.metadataManager.dirty[id]) {
      delete state.metadataManager.dirty[id][field];
      if (!Object.keys(state.metadataManager.dirty[id]).length) delete state.metadataManager.dirty[id];
    }
  } else {
    state.metadataManager.dirty[id] = { ...(state.metadataManager.dirty[id] || {}), [field]: value };
  }
  const row = document.querySelector(`.metadata-manager-input[data-id="${CSS.escape(id)}"]`)?.closest('tr');
  if (row) row.classList.toggle('metadata-row-dirty', !!state.metadataManager.dirty[id]);
  const dirtyCount = Object.keys(state.metadataManager.dirty || {}).length;
  if ($('metadataManagerDirtyCount')) $('metadataManagerDirtyCount').textContent = `${dirtyCount} edited`;
  const editedStat = $('metadataManagerSummary')?.querySelector('.metadata-manager-stat:last-child strong');
  if (editedStat) editedStat.textContent = String(dirtyCount);
  if (field === 'associatedPlatforms') metadataManagerRefreshRowPreferredPlatformState(id);
}

function metadataManagerRefreshRowPreferredPlatformState(id) {
  const row = document.querySelector(`.metadata-manager-input[data-id="${CSS.escape(id)}"]`)?.closest('tr');
  if (!row) return;
  const item = (state.items || []).find(i => metadataManagerItemId(i) === id);
  if (!item || item.kind !== 'Strategy Guide') return;
  const associatedInput = row.querySelector('.metadata-manager-input[data-field="associatedPlatforms"]');
  const categoryInput = row.querySelector('.metadata-manager-input[data-field="category"]');
  if (!associatedInput || !categoryInput) return;
  const multiple = hasMultipleAssociatedPlatforms(itemArray(associatedInput.value));
  categoryInput.readOnly = multiple;
  categoryInput.classList.toggle('readonly', multiple);
  categoryInput.toggleAttribute('aria-readonly', multiple);
  if (multiple) {
    categoryInput.value = MULTI_PLATFORM_LABEL;
    categoryInput.title = 'Preferred Platform is read-only when Associated Platforms contains multiple systems.';
    if (state.metadataManager?.dirty?.[id]?.category) {
      delete state.metadataManager.dirty[id].category;
      if (!Object.keys(state.metadataManager.dirty[id]).length) delete state.metadataManager.dirty[id];
    }
  } else {
    categoryInput.title = '';
    if (platformNamesEqual(categoryInput.value, MULTI_PLATFORM_LABEL)) {
      categoryInput.value = item.category || item.system || item.primarySystem || '';
    }
  }
}

function metadataManagerSelectedItems() {
  const selected = new Set(state.metadataManager.selectedIds || []);
  return (state.items || []).filter(item => selected.has(metadataManagerItemId(item)));
}

function mergeTokenLists(existing, tokens, mode = 'add') {
  const base = itemArray(existing);
  const incoming = itemArray(tokens);
  if (mode === 'replace') return incoming;
  if (mode === 'remove') {
    const remove = new Set(incoming.map(v => v.toLowerCase()));
    return base.filter(v => !remove.has(String(v || '').toLowerCase()));
  }
  const seen = new Set();
  return [...base, ...incoming].map(v => String(v || '').trim()).filter(Boolean).filter(v => {
    const key = v.toLowerCase();
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

async function metadataManagerPersist(id, item, payload) {
  metadataManagerUpdateItemLocal(id, payload);
  const res = await fetch(`/api/items/${encodeURIComponent(id)}/metadata`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  if (!res.ok) {
    let message = `HTTP ${res.status}`;
    try { message = (await res.json()).error || message; } catch {}
    throw new Error(message);
  }
  let updated = null;
  try { updated = await res.json(); } catch {}
  if (updated) {
    const merged = mergeSavedMetadataClientSide((state.items || []).find(i => metadataManagerItemId(i) === id) || item, updated, payload);
    replaceItemInState(merged);
  }
}

async function metadataManagerSaveDirtyRows() {
  const dirty = { ...(state.metadataManager.dirty || {}) };
  const ids = Object.keys(dirty);
  if (!ids.length) {
    metadataManagerSetStatus('No edited rows to save.', '');
    return;
  }
  metadataManagerSetStatus(`Saving ${ids.length} edited row(s)...`);
  let saved = 0;
  for (const id of ids) {
    const item = (state.items || []).find(i => metadataManagerItemId(i) === id);
    if (!item) continue;
    const payload = metadataManagerRowPayload(item, dirty[id]);
    await metadataManagerPersist(id, item, payload);
    saved += 1;
    delete state.metadataManager.dirty[id];
  }
  metadataManagerSetStatus(`Saved ${saved} edited row(s).`, 'success');
  renderMetadataManager();
}

async function metadataManagerApplyBatch() {
  const items = metadataManagerSelectedItems();
  if (!items.length) {
    metadataManagerSetStatus('Select one or more rows first.', 'error');
    return;
  }
  const langRaw = $('metadataBatchLanguage')?.value.trim() || '';
  const lang = /^en(?:glish)?$/i.test(langRaw) ? 'English' : langRaw;
  const region = $('metadataBatchRegion')?.value.trim() || '';
  const category = $('metadataBatchCategory')?.value.trim() || '';
  const publisher = $('metadataBatchPublisher')?.value.trim() || '';
  const tagText = $('metadataBatchTags')?.value || '';
  const tagMode = $('metadataBatchTagsMode')?.value || 'add';
  const hasTags = itemArray(tagText).length > 0;
  if (!lang && !region && !category && !publisher && !hasTags) {
    metadataManagerSetStatus('Enter at least one batch value to apply.', 'error');
    return;
  }
  metadataManagerSetStatus(`Applying batch metadata to ${items.length} selected row(s)...`);
  let saved = 0;
  for (const item of items) {
    const id = metadataManagerItemId(item);
    const payload = { metadataSource: 'Bulk metadata manager' };
    if (lang) payload.languageTag = lang;
    if (region) payload.region = region;
    if (category) { payload.category = category; payload.system = category; if (item.kind === 'Magazine') payload.primarySystem = category; }
    if (publisher) { payload.publisher = publisher; if (item.kind === 'Manual' || item.kind === 'Strategy Guide') payload.gamePublisher = publisher; }
    if (hasTags) {
      const topicField = metadataManagerTopicField(item);
      payload[topicField] = mergeTokenLists(item[topicField] || item.tags || [], tagText, tagMode);
    }
    await metadataManagerPersist(id, item, payload);
    saved += 1;
  }
  state.metadataManager.dirty = {};
  metadataManagerSetStatus(`Applied batch metadata to ${saved} row(s).`, 'success');
  renderMetadataManager();
}

async function metadataManagerNormalizeSelected() {
  const items = metadataManagerSelectedItems();
  if (!items.length) {
    metadataManagerSetStatus('Select rows to normalize first.', 'error');
    return;
  }
  metadataManagerSetStatus(`Normalizing ${items.length} selected row(s)...`);
  let saved = 0;
  let titleUpdates = 0;
  for (const item of items) {
    const id = metadataManagerItemId(item);
    const beforeName = metadataManagerItemName(item);
    const payload = metadataManagerNormalizedPayload(item);
    const afterName = payload.title || beforeName;
    if (String(afterName || '').trim() && String(afterName || '').trim() !== String(beforeName || '').trim()) titleUpdates += 1;
    await metadataManagerPersist(id, item, payload);
    saved += 1;
  }
  metadataManagerSetStatus(`Normalized ${saved} selected row(s). Updated ${titleUpdates} title/name value(s), filled missing language as English, kept region as US, and cleaned duplicate topic/tag values.`, 'success');
  renderMetadataManager();
}

function metadataManagerCsvValue(value) {
  const text = String(value ?? '');
  return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

function metadataManagerExportCsv() {
  const rows = metadataManagerEditableItems();
  const columns = metadataManagerVisibleColumns();
  const header = ['id', ...columns.map(column => column.key)];
  const lines = [header.join(',')];
  rows.forEach(item => {
    const values = [metadataManagerItemId(item), ...columns.map(column => metadataManagerFieldValue(item, column.key))];
    lines.push(values.map(metadataManagerCsvValue).join(','));
  });
  const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = `guidevault-metadata-${new Date().toISOString().slice(0,10)}.csv`;
  document.body.appendChild(link);
  link.click();
  URL.revokeObjectURL(link.href);
  link.remove();
  metadataManagerSetStatus(`Exported ${rows.length} visible row(s) with ${columns.length} visible column(s) to CSV.`, 'success');
}

function metadataManagerImportJsonFile(file) {
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => {
    try {
      const parsed = JSON.parse(String(reader.result || '[]'));
      const entries = Array.isArray(parsed) ? parsed : (Array.isArray(parsed.items) ? parsed.items : []);
      let staged = 0;
      entries.forEach(entry => {
        const id = String(entry.id || entry.Id || '').trim();
        const item = (state.items || []).find(i => metadataManagerItemId(i) === id);
        if (!id || !item) return;
        const changes = {};
        ['languageTag','region','year'].forEach(field => { if (entry[field] !== undefined) changes[field] = String(entry[field] ?? ''); });
        if (entry.title !== undefined || entry.name !== undefined) changes.name = String(entry.title ?? entry.name ?? '');
        if (entry.category !== undefined || entry.system !== undefined) changes.category = String(entry.category ?? entry.system ?? '');
        if (entry.series !== undefined) changes.series = String(entry.series ?? '');
        if (entry.publisher !== undefined) changes.publisher = String(entry.publisher ?? '');
        if (entry.topics !== undefined || entry.tags !== undefined) changes.topics = Array.isArray(entry.topics || entry.tags) ? (entry.topics || entry.tags).join(', ') : String(entry.topics ?? entry.tags ?? '');
        if (Object.keys(changes).length) {
          state.metadataManager.dirty[id] = { ...(state.metadataManager.dirty[id] || {}), ...changes };
          staged += 1;
        }
      });
      metadataManagerSetStatus(`Imported ${staged} matching metadata row(s). Review and click Save Edited Rows.`, 'success');
      renderMetadataManager();
    } catch (err) {
      metadataManagerSetStatus(`Import failed: ${err?.message || err}`, 'error');
    }
  };
  reader.readAsText(file);
}

function metadataManagerScrapePlaceholder() {
  metadataManagerSetStatus('Scraper/provider hooks are staged here for a future pass. For now, export CSV or import reviewed JSON metadata into the grid.', 'success');
}

function mergeSavedMetadataClientSide(baseItem, serverItem, payload) {
  const merged = { ...(baseItem || {}), ...(serverItem || {}) };
  if (!merged.id && merged.Id) merged.id = merged.Id;
  if (!merged.Id && merged.id) merged.Id = merged.id;
  const assign = (key, value) => {
    if (value !== undefined) merged[key] = value;
  };
  Object.entries(payload || {}).forEach(([key, value]) => assign(key, value));
  if (payload?.category !== undefined) merged.category = payload.category;
  if (payload?.system !== undefined) merged.system = payload.system;
  if (payload?.languageTag !== undefined) merged.languageTag = payload.languageTag;
  if (payload?.region !== undefined) merged.region = payload.region;
  if (payload?.isbn !== undefined) merged.isbn = payload.isbn;
  if (payload?.isbn10 !== undefined) merged.isbn10 = payload.isbn10;
  if (payload?.isbn13 !== undefined) merged.isbn13 = payload.isbn13;
  if (payload?.tags !== undefined) merged.tags = Array.isArray(payload.tags) ? payload.tags : [];
  if (payload?.associatedPlatforms !== undefined) merged.associatedPlatforms = Array.isArray(payload.associatedPlatforms) ? payload.associatedPlatforms : [];
  if (payload?.coveredPlatforms !== undefined) merged.coveredPlatforms = Array.isArray(payload.coveredPlatforms) ? payload.coveredPlatforms : [];
  if (payload?.coveredGames !== undefined) merged.coveredGames = Array.isArray(payload.coveredGames) ? payload.coveredGames : [];
  if (payload?.guideTopics !== undefined) merged.guideTopics = Array.isArray(payload.guideTopics) ? payload.guideTopics : [];
  if (payload?.specialFeatures !== undefined) merged.specialFeatures = Array.isArray(payload.specialFeatures) ? payload.specialFeatures : [];
  if (payload?.includedExtras !== undefined) merged.includedExtras = Array.isArray(payload.includedExtras) ? payload.includedExtras : [];
  if (payload?.charactersCovered !== undefined) merged.charactersCovered = Array.isArray(payload.charactersCovered) ? payload.charactersCovered : [];
  if (payload?.locationsCovered !== undefined) merged.locationsCovered = Array.isArray(payload.locationsCovered) ? payload.locationsCovered : [];
  if (payload?.featuredGames !== undefined) merged.featuredGames = Array.isArray(payload.featuredGames) ? payload.featuredGames : [];
  if (payload?.featuredPlatforms !== undefined) merged.featuredPlatforms = Array.isArray(payload.featuredPlatforms) ? payload.featuredPlatforms : [];
  if (payload?.includedSections !== undefined) merged.includedSections = Array.isArray(payload.includedSections) ? payload.includedSections : [];
  if (payload?.itemsCovered !== undefined) merged.itemsCovered = Array.isArray(payload.itemsCovered) ? payload.itemsCovered : [];
  return merged;
}

function replaceItemInState(updated) {
  if (!updated) return;
  const updatedId = String(updated.id || updated.Id || '').trim();
  if (!updatedId) return;
  const index = state.items.findIndex(i => String(i.id || i.Id || '') === updatedId);
  if (index >= 0) state.items[index] = updated;
  else state.items.push(updated);
  if (state.selected && String(state.selected.id || state.selected.Id || '') === updatedId) state.selected = updated;
}

function cleanIsbnValue(value) {
  return String(value || '').replace(/[^0-9Xx]/g, '').toUpperCase();
}

function splitIsbnInput(value) {
  const parts = String(value || '')
    .split(/[;,|/]+/)
    .map(cleanIsbnValue)
    .filter(Boolean);
  let isbn10 = '';
  let isbn13 = '';
  for (const part of parts) {
    if (part.length === 13 && !isbn13) isbn13 = part;
    else if (part.length === 10 && !isbn10) isbn10 = part;
  }
  const single = cleanIsbnValue(value);
  if (!isbn13 && single.length === 13) isbn13 = single;
  if (!isbn10 && single.length === 10) isbn10 = single;
  return { isbn10, isbn13 };
}

function combinedIsbnText(item) {
  const raw = String(item?.isbn || item?.Isbn || '').trim();
  if (raw) return raw;
  const values = [item?.isbn13 || item?.Isbn13, item?.isbn10 || item?.Isbn10].map(v => String(v || '').trim()).filter(Boolean);
  return values.join(' / ');
}

function detailValue(value) {
  const raw = String(value ?? '').trim();
  return raw ? raw : '—';
}

function detailTagListHtml(value) {
  const values = itemArray(value);
  if (!values.length) return '<span class="muted-dash">—</span>';
  return `<div class="overview-chip-list">${values.map(v => `<span class="overview-chip">${escapeHtml(v)}</span>`).join('')}</div>`;
}

function chipListHtml(value) {
  const values = itemArray(value);
  if (!values.length) return '<span class="muted-dash">—</span>';
  return values.map(v => `<span class="overview-chip">${escapeHtml(v)}</span>`).join('');
}

function magazineOverviewField(label, value, className = '') {
  return `<div class="overview-field ${className}"><span>${escapeHtml(label)}</span><strong>${escapeHtml(detailValue(value))}</strong></div>`;
}

function magazineOverviewTagSection(label, value) {
  return `<section class="overview-tag-section"><h3>${escapeHtml(label)}</h3>${detailTagListHtml(value)}</section>`;
}

function magazineOverviewHtml(item) {
  const title = item.magazineTitle || item.series || detectedSystemOf(item);
  const issue = String(item.issueNumber || '').trim();
  const identityTags = [
    item.magazineCategory || 'Video Game Magazine',
    item.platformFocus || item.primarySystem || item.system,
    item.coverDate || item.publicationDate || item.year
  ].filter(Boolean);
  return `
    <div class="magazine-hero-overview">
      <div class="magazine-hero-kicker">Magazine</div>
      <h2>${escapeHtml(title || item.title || 'Video Game Magazine')}${issue ? ` <span>#${escapeHtml(issue)}</span>` : ''}</h2>
      <p>${escapeHtml(item.summary || descriptionFor(item))}</p>
      <div class="magazine-hero-tags">${identityTags.map(v => `<span class="overview-chip">${escapeHtml(v)}</span>`).join('')}</div>
    </div>
    <div class="magazine-overview-grid">
      <section class="overview-card overview-card-identity magazine-overview-card-no-heading">
        <div class="overview-field-grid">
          ${magazineOverviewField('Magazine', title, 'wide')}
          ${magazineOverviewField('Issue #', item.issueNumber)}
          ${magazineOverviewField('Volume', item.volume)}
          ${magazineOverviewField('Cover Date', item.coverDate || item.publicationDate || item.year)}
          ${magazineOverviewField('Publication Date', item.publicationDate || item.coverDate || item.year)}
          ${magazineOverviewField('Publisher', item.publisher)}
        </div>
      </section>
      <section class="overview-card overview-card-classification magazine-overview-card-no-heading">
        <div class="overview-field-grid">
          ${magazineOverviewField('Region', item.region)}
          ${magazineOverviewField('Language', item.languageTag)}
          ${magazineOverviewField('Platform Focus', item.platformFocus)}
          ${magazineOverviewField('Primary System', item.primarySystem || item.system)}
          ${magazineOverviewField('Category', item.magazineCategory || item.category || 'Video game magazine', 'wide')}
          ${magazineOverviewField('Cover Game / Subject', item.coverSubject, 'wide')}
        </div>
      </section>
      ${magazineOverviewTagSection('Featured Games', item.featuredGames)}
      ${magazineOverviewTagSection('Featured Platforms', item.featuredPlatforms)}
      ${magazineOverviewTagSection('Special Features', item.specialFeatures)}
      ${magazineOverviewTagSection('Included Extras', item.includedExtras)}
    </div>`;
}



function manualOverviewHtml(item) {
  const gameTitle = item.gameTitle || item.platformMatchTitle || item.series || item.title || '';
  const manualTitle = item.manualTitle || item.title || '';
  const manualType = item.manualType || 'Instruction Manual';
  const systemLabel = detailSystemLabelForItem(item);
  const identityTags = [manualType, systemLabel, item.year].filter(Boolean);
  const rating = String(item?.rating || '').trim();
  const esrbLabel = esrbDisplayLabel(rating);
  const esrbHtml = `<img class="manual-hero-esrb" src="${escapeHtml(esrbIconUrl(rating))}" alt="${escapeHtml(esrbLabel)}" title="${escapeHtml(esrbLabel)}" onerror="this.onerror=null;this.src='/assets/ESRB/ratednone.png';" />`;
  return `
    <div class="manual-hero-overview has-hero-esrb">
      <div class="manual-hero-copy">
        <div class="manual-hero-kicker">Manual</div>
        <h2>${escapeHtml(gameTitle || manualTitle || 'Game Manual')}</h2>
        <p>${escapeHtml(item.summary || descriptionFor(item))}</p>
        <div class="manual-hero-tags">${identityTags.map(v => `<span class="overview-chip">${escapeHtml(v)}</span>`).join('')}</div>
      </div>
      ${esrbHtml}
    </div>
    <div class="manual-overview-board">
      <section class="overview-card manual-identity-card">
        <div class="manual-field-grid">
          ${magazineOverviewField('Manual Title', manualTitle, 'wide')}
          ${magazineOverviewField('Game', gameTitle)}
          ${magazineOverviewField('Manual Type', manualType)}
          ${magazineOverviewField('System', systemLabel)}
          ${magazineOverviewField('Publisher', item.publisher)}
          ${magazineOverviewField('Year', item.year)}
          ${magazineOverviewField('Region', item.region)}
          ${magazineOverviewField('Language', item.languageTag)}
        </div>
      </section>
      <div class="manual-context-stack">
        <section class="overview-card manual-game-card">
          <div class="manual-field-grid">
            ${magazineOverviewField('Franchise / Series', item.franchise || item.series)}
            ${magazineOverviewField('Developer', item.developer)}
            ${magazineOverviewField('Game Publisher', item.gamePublisher)}
            ${magazineOverviewField('Game Release Year', item.gameReleaseYear)}
          </div>
        </section>
        <section class="overview-card manual-genre-card">
          <h3>Genre</h3>
          <div class="overview-chip-list">${chipListHtml(item.genre)}</div>
        </section>
      </div>
      <section class="overview-card manual-content-card">
        <h3>Manual Content</h3>
        <div class="manual-content-grid">
          ${magazineOverviewTagSection('Included Sections', item.includedSections)}
          ${magazineOverviewTagSection('Included Extras', item.includedExtras)}
          ${magazineOverviewTagSection('Characters Covered', item.charactersCovered)}
          ${magazineOverviewTagSection('Items / Mechanics', item.itemsCovered)}
          ${magazineOverviewField('Controls / Scheme', item.controlScheme, 'wide')}
          ${magazineOverviewField('Warranty / Support', item.warrantySupport, 'wide')}
        </div>
      </section>
    </div>`;
}


function strategyOverviewHtml(item) {
  const gameTitle = item.gameTitle || item.platformMatchTitle || item.series || item.title || '';
  const systemLabel = detailSystemLabelForItem(item);
  const identityTags = [item.guideType || 'Strategy Guide', systemLabel, item.year].filter(Boolean);
  const rating = String(item?.rating || '').trim();
  const showEsrb = isEsrbIconEligible(item);
  const esrbLabel = esrbDisplayLabel(rating);
  const esrbHtml = showEsrb
    ? `<img class="strategy-hero-esrb" src="${escapeHtml(esrbIconUrl(rating))}" alt="${escapeHtml(esrbLabel)}" title="${escapeHtml(esrbLabel)}" onerror="this.onerror=null;this.src='/assets/ESRB/ratednone.png';" />`
    : '';
  return `
    <div class="strategy-hero-overview${showEsrb ? ' has-hero-esrb' : ''}">
      <div class="strategy-hero-copy">
        <div class="strategy-hero-kicker">Strategy Guide</div>
        <h2>${escapeHtml(gameTitle || item.title || 'Strategy Guide')}</h2>
        <p>${escapeHtml(item.summary || descriptionFor(item))}</p>
        <div class="strategy-hero-tags">${identityTags.map(v => `<span class="overview-chip">${escapeHtml(v)}</span>`).join('')}</div>
      </div>
      ${esrbHtml}
    </div>
    <div class="strategy-overview-board">
      <section class="overview-card strategy-quick-card">
        <div class="strategy-snapshot-grid">
          ${magazineOverviewField('Guide Title', item.title, 'wide')}
          ${magazineOverviewField('Game', gameTitle)}
          ${magazineOverviewField('System', systemLabel)}
          ${magazineOverviewField('Publisher', item.publisher)}
          ${magazineOverviewField('Author', item.writer)}
          ${magazineOverviewField('Year', item.year)}
          ${magazineOverviewField('Edition', item.edition)}
          ${magazineOverviewField('Region', item.region)}
          ${magazineOverviewField('Language', item.languageTag)}
        </div>
      </section>
      <div class="strategy-context-stack">
        <section class="overview-card strategy-game-card">
          <div class="strategy-context-list">
            ${magazineOverviewField('Franchise / Series', item.franchise || item.series)}
            ${magazineOverviewField('Developer', item.developer)}
            ${magazineOverviewField('Game Publisher', item.gamePublisher)}
            ${magazineOverviewField('Game Release Year', item.gameReleaseYear)}
            ${magazineOverviewField('ISBN', combinedIsbnText(item), 'wide')}
          </div>
        </section>
        <section class="overview-card strategy-genre-card">
          <h3>Genre</h3>
          <div class="overview-chip-list">${chipListHtml(item.genre)}</div>
        </section>
      </div>
      <section class="overview-card strategy-content-card">
        <h3>Guide Content</h3>
        <div class="strategy-content-grid">
          ${magazineOverviewTagSection('Guide Topics', item.guideTopics)}
          ${magazineOverviewTagSection('Special Features', item.specialFeatures)}
          ${magazineOverviewTagSection('Included Extras', item.includedExtras)}
          ${magazineOverviewTagSection('Covered Games', item.coveredGames && item.coveredGames.length ? item.coveredGames : (gameTitle ? [gameTitle] : []))}
          ${magazineOverviewTagSection('Covered Platforms', item.coveredPlatforms && item.coveredPlatforms.length ? item.coveredPlatforms : item.associatedPlatforms)}
          ${magazineOverviewTagSection('Characters Covered', item.charactersCovered)}
          ${magazineOverviewTagSection('Locations Covered', item.locationsCovered)}
        </div>
      </section>
    </div>`;
}

function magazineTechnicalRows(item) {
  const sourceFile = item.fileName || (item.path ? String(item.path).split(/[\\/]/).pop() : '—');
  const libraryPath = item.libraryName || item.libraryType || '—';
  const scanStatus = item.validationStatus && item.validationStatus !== 'ok' ? item.validationStatus : 'OK';
  const fileSize = Number(item.sizeBytes || 0) > 0 ? `${Math.round(Number(item.sizeBytes) / 1024 / 1024 * 10) / 10} MB` : '—';
  const modified = item.modified ? new Date(item.modified).toLocaleString() : '—';
  return [
    ['Page Count', itemPageCountLabel(item)],
    ['File Format', item.format || '—'],
    ['Source File', sourceFile],
    ['Library', libraryPath],
    ['Source Path', item.path || '—'],
    ['File Size', fileSize],
    ['Modified Date', modified],
    ['Scan Status', scanStatus],
    ...(item.validationMessage ? [['Validation Message', item.validationMessage]] : []),
    ...(item.metadataSource ? [['Metadata Source', item.metadataSource]] : [])
  ];
}

function updateMetadataTechnicalInfo(item) {
  const panel = $('metadataTechnicalInfo');
  if (!panel) return;
  const rows = magazineTechnicalRows(item);
  panel.classList.toggle('hidden', !rows.length);
  if (!rows.length) {
    panel.innerHTML = '';
    return;
  }
  panel.innerHTML = `
    <details class="metadata-library-data-dropdown">
      <summary>
        <span>Library Data</span>
        <em>${rows.length} field${rows.length === 1 ? '' : 's'}</em>
      </summary>
      <dl class="metadata-technical-list">
        ${rows.map(([k, v]) => metaRow(k, v, false)).join('')}
      </dl>
    </details>`;
}

function isEsrbIconEligible(item) {
  const kind = String(item?.kind || '').trim().toLowerCase();
  return kind === 'strategy guide';
}

function normalizeEsrbRating(value) {
  const raw = String(value || '').trim().toLowerCase();
  if (!raw) return 'none';
  const compact = raw.replace(/[^a-z0-9+]/g, '');
  if (!compact || ['none', 'na', 'n/a', 'nr', 'notrated', 'unrated', 'norating', 'ratingnone'].includes(raw) || ['none','na','nr','notrated','unrated','norating','ratingnone'].includes(compact)) return 'none';
  if (compact === 'e' || compact === 'everyone') return 'e';
  if (compact === 'e10' || compact === 'e10+' || compact === 'everyone10' || compact === 'everyone10+') return 'e10';
  if (compact === 't' || compact === 'teen') return 't';
  if (compact === 'm' || compact === 'mature' || compact === 'mature17' || compact === 'mature17+') return 'm';
  if (compact === 'ao' || compact === 'adultsonly' || compact === 'adultsonly18' || compact === 'adultsonly18+') return 'ao';
  if (compact === 'rp' || compact === 'ratingpending') return 'rp';
  if (compact === 'ec' || compact === 'earlychildhood') return 'ec';
  if (compact === 'ka' || compact === 'kidstoadults' || compact === 'kidsadults') return 'ka';
  return 'none';
}

function esrbIconUrl(value) {
  const key = normalizeEsrbRating(value);
  const file = {
    e: 'ratede.png',
    e10: 'ratede10.png',
    t: 'ratedt.png',
    m: 'ratedm.png',
    ao: 'ratedao.png',
    rp: 'ratedrp.png',
    ec: 'ratedec.png',
    ka: 'ratedka.png',
    none: 'ratednone.png'
  }[key] || 'ratednone.png';
  return `/assets/ESRB/${file}`;
}

function esrbDisplayLabel(value) {
  const key = normalizeEsrbRating(value);
  const labels = {
    e: 'Everyone',
    e10: 'Everyone 10+',
    t: 'Teen',
    m: 'Mature 17+',
    ao: 'Adults Only 18+',
    rp: 'Rating Pending',
    ec: 'Early Childhood',
    ka: 'Kids to Adults',
    none: 'Not Rated'
  };
  return labels[key] || 'Not Rated';
}

function updateDetailEsrbIcon(item) {
  const icon = $('detailEsrbIcon');
  const panel = $('overviewPanel');
  if (!icon) return;
  const show = isEsrbIconEligible(item);
  const useHeroIcon = ['strategy guide', 'manual'].includes(String(item?.kind || '').trim().toLowerCase());
  icon.classList.toggle('hidden', !show || useHeroIcon);
  if (panel) panel.classList.toggle('has-esrb-icon', show && !useHeroIcon);
  if (!show) {
    icon.removeAttribute('src');
    icon.removeAttribute('title');
    return;
  }
  const rating = String(item?.rating || '').trim();
  icon.src = esrbIconUrl(rating);
  icon.title = esrbDisplayLabel(rating);
  icon.onerror = () => {
    if (!icon.src.endsWith('/ratednone.png')) icon.src = '/assets/ESRB/ratednone.png';
  };
}


function syncPreferredPlatformEditorState() {
  const kind = $('editKind')?.value || state.selected?.kind || '';
  const categoryInput = $('editCategory');
  if (!categoryInput) return;
  const platforms = itemArray($('editAssociatedPlatforms')?.value || '');
  const readOnly = kind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(platforms);
  categoryInput.readOnly = readOnly;
  categoryInput.classList.toggle('metadata-derived-readonly', readOnly);
  categoryInput.toggleAttribute('aria-readonly', readOnly);
  if (readOnly) {
    categoryInput.value = MULTI_PLATFORM_LABEL;
    categoryInput.title = 'Preferred Platform is read-only because this guide appears under multiple Associated Platforms.';
  } else {
    categoryInput.title = '';
    if (platformNamesEqual(categoryInput.value, MULTI_PLATFORM_LABEL)) {
      categoryInput.value = platforms.length === 1 ? platforms[0] : (state.selected?.category || state.selected?.system || state.selected?.primarySystem || '');
    }
  }
}


function itemIdOf(item) {
  return String(item?.id || item?.Id || '').trim();
}

function compareDetailSequence(a, b) {
  if (!a || !b) return 0;
  const kind = String(a.kind || '').trim();
  if (kind === 'Manual') return displayTitle(a).localeCompare(displayTitle(b));
  return itemSequenceThenTitle(a, b);
}

function detailNavigationSource(item) {
  if (!item) return [];
  const currentId = itemIdOf(item);
  const currentKind = String(item.kind || '').trim();

  // Prefer the current filtered/category/search result if the selected item
  // came from that result set. This keeps Previous/Next aligned with the shelf
  // the user was browsing.
  let source = Array.isArray(state.filtered)
    ? state.filtered.filter(i => String(i.kind || '').trim() === currentKind)
    : [];

  if (!source.some(i => itemIdOf(i) === currentId)) {
    source = (Array.isArray(state.items) ? state.items : [])
      .filter(i => String(i.kind || '').trim() === currentKind);
  }

  const seen = new Set();
  return source
    .filter(i => {
      const id = itemIdOf(i);
      if (!id || seen.has(id)) return false;
      seen.add(id);
      return true;
    })
    .sort(compareDetailSequence);
}

function adjacentDetailItems(item) {
  const source = detailNavigationSource(item);
  const currentId = itemIdOf(item);
  const index = source.findIndex(i => itemIdOf(i) === currentId);
  return {
    previous: index > 0 ? source[index - 1] : null,
    next: index >= 0 && index < source.length - 1 ? source[index + 1] : null
  };
}

function updateDetailNavigationButtons(item) {
  const prev = $('detailPrevBtn');
  const next = $('detailNextBtn');
  const wrap = $('detailNavControls');
  if (!prev || !next) return;

  const adjacent = adjacentDetailItems(item);
  prev.classList.toggle('hidden', !adjacent.previous);
  next.classList.toggle('hidden', !adjacent.next);
  if (wrap) wrap.classList.toggle('hidden', !adjacent.previous && !adjacent.next);

  if (adjacent.previous) {
    prev.dataset.itemId = itemIdOf(adjacent.previous);
    prev.title = `Previous: ${displayTitle(adjacent.previous)}`;
  } else {
    prev.removeAttribute('data-item-id');
    prev.removeAttribute('title');
  }

  if (adjacent.next) {
    next.dataset.itemId = itemIdOf(adjacent.next);
    next.title = `Next: ${displayTitle(adjacent.next)}`;
  } else {
    next.removeAttribute('data-item-id');
    next.removeAttribute('title');
  }
}

function navigateDetailAdjacent(direction) {
  if (!state.selected) return;
  const adjacent = adjacentDetailItems(state.selected);
  const target = direction === 'previous' ? adjacent.previous : adjacent.next;
  if (!target) return;
  showDetailScreen(target);
}


function renderDetails(item) {
  item = applyClientMetadataOverride(item);
  if (!item || !$('detailCover')) return;
  document.body.classList.toggle('strategy-detail-mode', item.kind === 'Strategy Guide');
  document.body.classList.toggle('magazine-detail-mode', item.kind === 'Magazine');
  document.body.classList.toggle('manual-detail-mode', item.kind === 'Manual');
  $('detailCover').src = coverUrl(item);
  applyColorscapeToDetail(item);
  if ($('readBtn')) $('readBtn').dataset.itemId = item.id || '';
  $('detailCover').classList.toggle('nes-detail-cover', specialCardClass(item).includes('nes-manual-card'));
  $('detailTitle').textContent = displayTitle(item);
  updateDetailNavigationButtons(item);
  const detectedSystem = detailSystemLabelForItem(item);
  $('detailSub').textContent = `${detectedSystem} • ${item.year || 'Unknown'}`;
  $('description').textContent = item.summary || descriptionFor(item);
  $('description').classList.toggle('hidden', item.kind === 'Magazine' || item.kind === 'Strategy Guide' || item.kind === 'Manual');
  $('tagList').innerHTML = (item.tags || []).map(t => `<span class="tag">${escapeHtml(t)}</span>`).join('');
  updateDetailEsrbIcon(item);
  const webLink = normalizeWebLink(item.webLink);
  if ($('editAssociatedPlatformsLabel')) $('editAssociatedPlatformsLabel').classList.toggle('hidden', item.kind !== 'Strategy Guide');
  if ($('editPlatformMatchLabel')) $('editPlatformMatchLabel').classList.toggle('hidden', item.kind !== 'Strategy Guide');
  const metaEl = $('meta');
  if (item.kind === 'Magazine') {
    if (metaEl) {
      metaEl.className = 'magazine-overview';
      metaEl.innerHTML = magazineOverviewHtml(item);
    }
  } else if (item.kind === 'Strategy Guide') {
    if (metaEl) {
      metaEl.className = 'magazine-overview strategy-overview';
      metaEl.innerHTML = strategyOverviewHtml(item);
    }
  } else if (item.kind === 'Manual') {
    if (metaEl) {
      metaEl.className = 'magazine-overview manual-overview';
      metaEl.innerHTML = manualOverviewHtml(item);
    }
  } else {
    const metaRows = [
      ['Detected System', detectedSystem],
      ...(webLink ? [['Web Link', `<a class="meta-link" href="${escapeHtml(webLink)}" target="_blank" rel="noopener noreferrer">${escapeHtml(item.webLink || webLink)}</a>`, true]] : []),
      ['Publisher', item.publisher || '—'],
      ['Year', item.year || '—'],
      ['Writer', item.writer || '—'],
      ['Pages', itemPageCountLabel(item)]
    ];
    if (metaEl) {
      metaEl.className = 'detail-meta-list';
      metaEl.innerHTML = metaRows.map(([k, v, html]) => metaRow(k, v, !!html)).join('');
    }
  }
  updateMetadataTechnicalInfo(item);
  $('editTitle').value = item.title || '';
  $('editKind').value = item.kind || 'Manual';
  $('editCategory').value = preferredPlatformOf(item) || categoryOf(item) || '';
  if ($('editAssociatedPlatforms')) $('editAssociatedPlatforms').value = platformListText(item);
  syncPreferredPlatformEditorState();
  if ($('editPlatformMatchTitle')) $('editPlatformMatchTitle').value = item.platformMatchTitle || '';
  if ($('editEsrbRating')) $('editEsrbRating').value = item.kind === 'Magazine' ? '' : (item.rating || '');
  if ($('editEsrbRatingLabel')) $('editEsrbRatingLabel').classList.toggle('hidden', item.kind === 'Magazine');
  $('editSeries').value = item.series || '';
  const isMagazine = item.kind === 'Magazine';
  const isStrategyGuide = item.kind === 'Strategy Guide';
  const isManual = item.kind === 'Manual';
  updateTypedMetadataFieldVisibility(item.kind || '');
  setMaybeValue('editMagazineTitle', isMagazine ? (item.magazineTitle || item.series || detectedSystem) : '');
  $('editIssue').value = isMagazine ? (item.issueNumber || '') : '';
  if ($('editIssueLabel')) $('editIssueLabel').classList.toggle('hidden', !isMagazine);
  setMaybeValue('editVolume', isMagazine ? item.volume : '');
  setMaybeValue('editCoverDate', isMagazine ? item.coverDate : '');
  setMaybeValue('editPublicationDate', isMagazine ? item.publicationDate : '');
  $('editPublisher').value = item.publisher || '';
  setMaybeValue('editRegion', (isMagazine || isStrategyGuide || isManual) ? item.region : '');
  setMaybeValue('editLanguageTag', (isMagazine || isStrategyGuide || isManual) ? item.languageTag : '');
  setMaybeValue('editPlatformFocus', isMagazine ? item.platformFocus : '');
  setMaybeValue('editPrimarySystem', isMagazine ? item.primarySystem : '');
  setMaybeValue('editMagazineCategory', isMagazine ? item.magazineCategory : '');
  setMaybeValue('editCoverSubject', isMagazine ? item.coverSubject : '');
  setMaybeValue('editGameTitle', (isStrategyGuide || isManual) ? (item.gameTitle || item.platformMatchTitle || item.series || '') : '');
  setMaybeValue('editIsbn', isStrategyGuide ? combinedIsbnText(item) : '');
  setMultiSelectValues('editGuideType', isStrategyGuide ? (item.guideType || 'Strategy Guide') : '');
  const editionType = isStrategyGuide ? (item.editionType || inferEditionTypes(item.edition)) : [];
  setMultiSelectValues('editEditionType', editionType);
  setMaybeValue('editEditionYear', isStrategyGuide ? (item.editionYear || inferEditionYear(item.edition)) : '');
  setMaybeValue('editEditionVolume', isStrategyGuide ? (item.editionVolume || inferEditionVolume(item.edition)) : '');
  updateEditionControls();
  setMaybeValue('editFranchise', (isStrategyGuide || isManual) ? (item.franchise || item.series || '') : '');
  setMaybeValue('editPublicationDateGuide', (isStrategyGuide || isManual) ? item.publicationDate : '');
  setMaybeValue('editDeveloper', (isStrategyGuide || isManual) ? item.developer : '');
  setMaybeValue('editGamePublisher', (isStrategyGuide || isManual) ? item.gamePublisher : '');
  setMaybeValue('editGameReleaseYear', (isStrategyGuide || isManual) ? item.gameReleaseYear : '');
  setMaybeValue('editGenre', (isStrategyGuide || isManual) ? item.genre : '');
  $('editYear').value = item.year || '';
  $('editWriter').value = item.writer || '';
  $('editSummary').value = item.summary || '';
  setMaybeValue('editFeaturedGames', isMagazine ? itemList(item.featuredGames).replace(/^—$/, '') : '');
  setMaybeValue('editFeaturedPlatforms', isMagazine ? itemList(item.featuredPlatforms).replace(/^—$/, '') : '');
  setMaybeValue('editSpecialFeatures', isMagazine ? itemList(item.specialFeatures).replace(/^—$/, '') : '');
  setMaybeValue('editIncludedExtras', (isMagazine || isStrategyGuide || isManual) ? itemList(item.includedExtras).replace(/^—$/, '') : '');
  setMaybeValue('editCoveredGames', isStrategyGuide ? itemList(item.coveredGames).replace(/^—$/, '') : '');
  setMaybeValue('editCoveredPlatforms', isStrategyGuide ? itemList(item.coveredPlatforms || item.associatedPlatforms).replace(/^—$/, '') : '');
  setMaybeValue('editGuideTopics', isStrategyGuide ? itemList(item.guideTopics).replace(/^—$/, '') : '');
  setMaybeValue('editStrategySpecialFeatures', isStrategyGuide ? itemList(item.specialFeatures).replace(/^—$/, '') : '');
  setMaybeValue('editCharactersCovered', (isStrategyGuide || isManual) ? itemList(item.charactersCovered).replace(/^—$/, '') : '');
  setMaybeValue('editLocationsCovered', isStrategyGuide ? itemList(item.locationsCovered).replace(/^—$/, '') : '');
  setMaybeValue('editManualTitle', isManual ? (item.manualTitle || item.title || '') : '');
  setMaybeValue('editManualType', isManual ? (item.manualType || 'Instruction Manual') : '');
  setMaybeValue('editIncludedSections', isManual ? itemList(item.includedSections).replace(/^—$/, '') : '');
  setMaybeValue('editControlScheme', isManual ? item.controlScheme : '');
  setMaybeValue('editItemsCovered', isManual ? itemList(item.itemsCovered).replace(/^—$/, '') : '');
  setMaybeValue('editWarrantySupport', isManual ? item.warrantySupport : '');
  $('editTags').value = (item.tags || []).join(', ');
  $('notesText').value = item.notes || '';
  renderDetailReadingProfilePanel(item);
}

function descriptionFor(item) {
  if (item.kind === 'Magazine') return 'A gaming magazine issue grouped by magazine series/publication, sorted by ComicInfo issue number when available.';
  if (item.kind === 'Strategy Guide') return 'A strategy guide grouped by system inside the Strategy Guides section, separate from manuals.';
  return 'A game manual or instruction booklet grouped by system inside the Manuals section.';
}

async function saveSelectedMetadata(extra = {}, options = {}) {
  if (!state.selected) return null;
  const saveButton = options.button || null;
  const originalButtonText = saveButton?.textContent || '';
  const selectedId = String(state.selected.id || state.selected.Id || '').trim();

  try {
    if (saveButton) {
      saveButton.disabled = true;
      saveButton.textContent = 'Saving...';
    }

    const tags = $('editTags').value.split(',').map(t => t.trim()).filter(Boolean);
    const selectedKind = $('editKind').value;
    const associatedPlatforms = $('editAssociatedPlatforms') ? $('editAssociatedPlatforms').value.split(',').map(p => p.trim()).filter(Boolean) : [];
    const preferredPlatform = selectedKind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(associatedPlatforms)
      ? MULTI_PLATFORM_LABEL
      : ($('editCategory').value || '');
    const magazinePayload = selectedKind === 'Magazine' ? {
      magazineTitle: $('editMagazineTitle')?.value || $('editSeries').value || '',
      volume: $('editVolume')?.value || '',
      coverDate: $('editCoverDate')?.value || '',
      publicationDate: $('editPublicationDate')?.value || '',
      region: $('editRegion')?.value || '',
      platformFocus: $('editPlatformFocus')?.value || '',
      primarySystem: $('editPrimarySystem')?.value || '',
      magazineCategory: $('editMagazineCategory')?.value || '',
      coverSubject: $('editCoverSubject')?.value || '',
      featuredGames: csvInput('editFeaturedGames'),
      featuredPlatforms: csvInput('editFeaturedPlatforms'),
      specialFeatures: csvInput('editSpecialFeatures'),
      includedExtras: csvInput('editIncludedExtras')
    } : {};
    const rawIsbn = $('editIsbn')?.value || '';
    const isbnParts = selectedKind === 'Strategy Guide' ? splitIsbnInput(rawIsbn) : { isbn10: '', isbn13: '' };
    const strategyPayload = selectedKind === 'Strategy Guide' ? {
      gameTitle: $('editGameTitle')?.value || $('editPlatformMatchTitle')?.value || '',
      isbn: rawIsbn,
      isbn10: isbnParts.isbn10,
      isbn13: isbnParts.isbn13,
      guideType: getMultiSelectValues('editGuideType').join(', ') || 'Strategy Guide',
      editionType: getMultiSelectValues('editEditionType').join(', '),
      editionYear: $('editEditionYear')?.value || '',
      editionVolume: $('editEditionVolume')?.value || '',
      edition: buildEditionValue(getMultiSelectValues('editEditionType'), $('editEditionYear')?.value || '', $('editEditionVolume')?.value || ''),
      franchise: $('editFranchise')?.value || $('editSeries')?.value || '',
      publicationDate: $('editPublicationDateGuide')?.value || '',
      region: $('editRegion')?.value || '',
      developer: $('editDeveloper')?.value || '',
      gamePublisher: $('editGamePublisher')?.value || '',
      gameReleaseYear: $('editGameReleaseYear')?.value || '',
      genre: $('editGenre')?.value || '',
      coveredGames: csvInput('editCoveredGames'),
      coveredPlatforms: csvInput('editCoveredPlatforms'),
      guideTopics: csvInput('editGuideTopics'),
      specialFeatures: csvInput('editStrategySpecialFeatures'),
      includedExtras: csvInput('editIncludedExtras'),
      charactersCovered: csvInput('editCharactersCovered'),
      locationsCovered: csvInput('editLocationsCovered'),
      metadataSource: 'Manual edit'
    } : {};
    const manualPayload = selectedKind === 'Manual' ? {
      manualTitle: $('editManualTitle')?.value || $('editTitle')?.value || '',
      manualType: $('editManualType')?.value || 'Instruction Manual',
      gameTitle: $('editGameTitle')?.value || $('editSeries')?.value || $('editTitle')?.value || '',
      publicationDate: $('editPublicationDateGuide')?.value || '',
      region: $('editRegion')?.value || '',
      franchise: $('editFranchise')?.value || $('editSeries')?.value || '',
      developer: $('editDeveloper')?.value || '',
      gamePublisher: $('editGamePublisher')?.value || '',
      gameReleaseYear: $('editGameReleaseYear')?.value || $('editYear')?.value || '',
      genre: $('editGenre')?.value || '',
      includedSections: csvInput('editIncludedSections'),
      includedExtras: csvInput('editIncludedExtras'),
      controlScheme: $('editControlScheme')?.value || '',
      charactersCovered: csvInput('editCharactersCovered'),
      itemsCovered: csvInput('editItemsCovered'),
      warrantySupport: $('editWarrantySupport')?.value || '',
      metadataSource: 'Manual edit'
    } : {};

    const payload = {
      title: $('editTitle').value,
      kind: selectedKind,
      category: preferredPlatform,
      system: preferredPlatform,
      associatedPlatforms: selectedKind === 'Strategy Guide' ? associatedPlatforms : [],
      platformMatchTitle: selectedKind === 'Strategy Guide' ? ($('editPlatformMatchTitle')?.value || '') : '',
      series: $('editSeries').value,
      issueNumber: selectedKind === 'Magazine' ? $('editIssue').value : '',
      publisher: $('editPublisher').value,
      year: $('editYear').value,
      writer: $('editWriter').value,
      rating: selectedKind === 'Magazine' ? '' : ($('editEsrbRating')?.value || ''),
      summary: $('editSummary').value,
      tags,
      notes: $('notesText').value,
      languageTag: ($('editLanguageTag')?.value || ''),
      ...magazinePayload,
      ...strategyPayload,
      ...manualPayload,
      ...extra
    };

    // Keep an exact client-side snapshot of what the user submitted. The server
    // response can still contain derived scan metadata, but it should never be
    // allowed to immediately overwrite the just-entered form values.
    const submitted = normalizeClientMetadataPayload(payload);
    const optimistic = mergeSavedMetadataClientSide(state.selected, {}, submitted);
    if (selectedId) {
      optimistic.id = optimistic.id || selectedId;
      optimistic.Id = optimistic.Id || selectedId;
    }

    // Update the visible detail screen immediately. The server save below should
    // also be fast, but the UI should not wait on disk/network/archive/index locks
    // just to keep typed text visible.
    replaceItemInState(optimistic);
    state.selected = optimistic;
    rememberClientMetadataOverride(selectedId || state.selected, submitted);
    renderDetails(optimistic);
    activateTab(options.tab || state.activeTab || 'metadata');

    const updateFilteredListOnly = () => {
      const q = ($('search')?.value || '').trim().toLowerCase();
      state.filtered = state.items.filter(item => {
        const matchesFilter = state.filter === 'All Content' || (state.filter === 'Favorites' ? isFavoriteItem(item) : item.kind === state.filter);
        const matchesCategory = itemMatchesCategoryFilter(item);
        const haystack = [item.title, item.kind, item.system, categoryOf(item), item.publisher, item.year, item.series, item.writer, item.issueNumber, item.asin, item.isbn, item.isbn10, item.isbn13, item.languageTag, platformListText(item), item.platformMatchTitle, item.platformResolverSource, item.summary, item.notes, item.relativePath, item.manualTitle, item.manualType, item.controlScheme, item.warrantySupport, ...(item.includedSections || []), ...(item.itemsCovered || []), ...(item.tags || [])].join(' ').toLowerCase();
        const matchesCustom = !state.customFilter || customSideNavItemMatches(item, state.customFilter);
    return matchesFilter && matchesCategory && matchesCustom && (!q || haystack.includes(q));
      });
    };
    updateFilteredListOnly();

    // Report success immediately after the local snapshot is applied. The
    // network save continues in the background and should not make text metadata
    // editing feel slow.
    if (saveButton) {
      saveButton.textContent = 'Saved';
      saveButton.disabled = false;
      window.setTimeout(() => {
        if (saveButton) saveButton.textContent = originalButtonText || 'Save Metadata';
      }, 900);
    }

    fetch(`/api/items/${encodeURIComponent(selectedId)}/metadata`, {
      method: 'PUT',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify(submitted)
    })
      .then(async res => {
        if (!res.ok) {
          let message = 'Unable to save metadata';
          try { message = (await res.json()).error || message; } catch {}
          throw new Error(message);
        }
        let serverUpdated = null;
        try { serverUpdated = await res.json(); } catch { serverUpdated = {}; }

        const latestBase = state.items.find(i => String(i.id || i.Id || '') === String(selectedId)) || optimistic;
        const updated = mergeSavedMetadataClientSide(latestBase, serverUpdated || {}, submitted);
        updated.id = updated.id || selectedId;
        updated.Id = updated.Id || selectedId;

        replaceItemInState(updated);
        if (state.selected && String(state.selected.id || state.selected.Id || '') === String(selectedId)) {
          state.selected = updated;
          renderDetails(updated);
          activateTab(options.tab || state.activeTab || 'metadata');
          updateFilteredListOnly();
        }
      })
      .catch(err => {
        console.error('Metadata background save failed', err);
        if (saveButton) saveButton.textContent = 'Save Failed';
        alert(`Unable to save metadata: ${err?.message || err}`);
      });

    return optimistic;
  } catch (err) {
    console.error('Metadata save failed', err);
    alert(`Unable to save metadata: ${err?.message || err}`);
    return null;
  } finally {
    if (saveButton) {
      window.setTimeout(() => { saveButton.disabled = false; }, 120);
    }
  }
}

async function resolveStrategyPlatforms() {
  if (!state.selected || state.selected.kind !== 'Strategy Guide') return;
  const btn = $('lookupPlatformsBtn');
  try {
    if (btn) { btn.classList.add('lookup-busy'); btn.textContent = 'Looking up platforms...'; }
    const res = await fetch(`/api/items/${state.selected.id}/strategy-platforms/resolve`, { method: 'POST', cache: 'no-store' });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) {
      const msg = data?.error || data?.message || `Platform lookup failed. HTTP ${res.status}`;
      alert(msg);
      return;
    }
    const updated = data?.item || data;
    if (updated?.id) {
      const idx = state.items.findIndex(i => i.id === updated.id);
      if (idx >= 0) state.items[idx] = updated;
      state.selected = updated;
      applyFilters();
      showDetailScreen(updated);
    }
    if (data?.resolution?.message) setStatus(data.resolution.message);
  } catch (err) {
    console.error(err);
    alert(`Platform lookup failed: ${err?.message || err}`);
  } finally {
    if (btn) { btn.classList.remove('lookup-busy'); btn.textContent = 'Lookup Strategy Platforms'; }
  }
}


function normalizeReaderDisplayMode(mode) {
  const numeric = Number(mode);
  return numeric === 1 ? 1 : numeric === 3 ? 3 : 2;
}

function isReaderAdaptiveSpreadMode() {
  return normalizeReaderDisplayMode(state.reader.displayMode) === 3;
}


function cleanupReaderResources(options = {}) {
  const keepState = options.keepState === true;
  window.clearTimeout(state.reader?.resizeTimer);
  window.clearTimeout(state.reader?.longPressTimer);
  state.reader.animating = false;
  state.reader.overlayVisible = false;
  state.reader.advancedVisible = false;
  state.reader.magnifierSettingsVisible = false;
  state.reader.magnifierActive = false;
  ['pageLeftImage', 'pageRightImage'].forEach(id => {
    const img = $(id);
    if (img) {
      img.removeAttribute('srcset');
      img.src = '';
    }
  });
  document.querySelectorAll('.turning-page, .reader-turn-overlay, .adaptive-turn-overlay').forEach(el => el.remove());
  if (!keepState) {
    state.reader.item = null;
    state.reader.pages = [];
    state.reader.index = 0;
  }
}
const GUIDEVAULT_APP_VIEW_IDS = ['libraryView', 'settingsView', 'detailView', 'readerView', 'profileView'];
function resetAppViewInlineState() {
  GUIDEVAULT_APP_VIEW_IDS.forEach(id => {
    const el = $(id);
    if (!el) return;
    el.style.removeProperty('display');
    el.style.removeProperty('visibility');
    el.style.removeProperty('position');
    el.style.removeProperty('z-index');
  });
}
function showAppView(id, display = '') {
  const el = $(id);
  if (!el) return;
  el.classList.remove('hidden');
  el.style.removeProperty('visibility');
  el.style.removeProperty('position');
  el.style.removeProperty('z-index');
  if (display) el.style.setProperty('display', display, 'important');
  else el.style.removeProperty('display');
}
function hideAppView(id) {
  const el = $(id);
  if (!el) return;
  el.classList.add('hidden');
  el.style.removeProperty('visibility');
  el.style.removeProperty('position');
  el.style.removeProperty('z-index');
  el.style.setProperty('display', 'none', 'important');
}

function cleanupInactiveViewsForNavigation(nextView = '') {
  if (nextView !== 'reader' && (state.reader?.pages?.length || state.reader?.item)) cleanupReaderResources();
  metadataManagerHandlePreviewPointerEnd?.();
  resetAppViewInlineState();
  if (coverPrimeObserver && document.querySelectorAll('img[data-cover-src]').length > 900) {
    try { coverPrimeObserver.disconnect(); } catch {}
    coverPrimeObserver = null;
  }
}

async function openReader(item) {
  if (!item) return;
  cleanupReaderResources();
  const format = String(item.format || item.Format || '').toUpperCase();
  if (format === 'PDF') { recordReadingActivity(item, 'read'); window.open(`/api/items/${item.id}/file`, '_blank'); return; }
  const res = await fetch(`/api/items/${item.id}/pages`, { cache: 'no-store' });
  let data = null;
  try { data = await res.json(); } catch {}
  if (!res.ok) {
    alert(data?.error || 'Unable to open this item in the reader.');
    return;
  }
  if (data?.pdfUrl) { window.open(data.pdfUrl, '_blank'); return; }
  const pages = Array.isArray(data?.pages) ? data.pages : [];
  if (!pages.length) {
    alert('No readable image pages were found for this item.');
    return;
  }
  recordReadingActivity(item, 'read');
  clearColorscapeDetailTheme();
  document.body.classList.remove('detail-page-mode', 'settings-sidebar-mode', 'profile-page-mode', 'strategy-detail-mode', 'magazine-detail-mode', 'manual-detail-mode');
  document.body.classList.add('reader-page-mode');
  state.reader.item = item; state.reader.pages = pages; state.reader.index = 0;
  applyReadingProfileToReader(item);
  state.reader.displayMode = normalizeReaderDisplayMode(state.reader.displayMode);
  state.reader.overlayVisible = false;
  state.reader.advancedVisible = false;
  state.reader.magnifierSettingsVisible = false;
  state.reader.magnifierActive = false;
  state.reader.shading = normalizeReaderShading(state.reader.shading || loadReaderShading());
  state.reader.magnifier = normalizeReaderMagnifier(state.reader.magnifier || loadReaderMagnifier());
  if ($('book')) $('book').classList.toggle('wide-manual', specialCardClass(item).includes('nes-manual-card'));
  applyReaderShadingSettings();
  applyReaderZoom();
  applyReaderBackground();
  updateReaderMagnifierControls();
  setReaderMagnifierActive(false);
  hideAppView('settingsView');
  hideAppView('profileView');
  hideAppView('detailView');
  hideAppView('libraryView');
  showAppView('readerView', 'flex');
  $('readerTitle').textContent = displayTitle(item) || item.title || '';
  await waitForReaderPaint();
  renderSpread(0, { preserveSize: false });
  updateReaderFullscreenUi();
  requestAnimationFrame(() => {
    document.body.classList.add('reader-page-mode');
    showAppView('readerView', 'flex');
    refreshReaderBookSize();
  });
}

function spreadForIndex(index) {
  if (!state.reader.pages.length) return null;
  const max = state.reader.pages.length - 1;
  const total = state.reader.pages.length;
  let normalized = Math.max(0, Math.min(index, max));
  const singlePageMode = state.reader.displayMode === 1;
  const adaptiveSpreadMode = isReaderAdaptiveSpreadMode();

  // Covers and explicit 1-page mode use a true single-page frame.  This avoids
  // showing any backing panel/border behind the front cover and gives the reader
  // a clean way to switch between one-page and two-page viewing.
  if (singlePageMode || normalized === 0) {
    return {
      isCover: normalized === 0,
      isSingle: true,
      index: normalized,
      leftIndex: null,
      rightIndex: normalized,
      rightUrl: state.reader.pages[normalized].imageUrl,
      isBlankRight: false,
      label: `Page ${normalized + 1} / ${total}`,
      positionText: `Page ${normalized + 1} of ${total}`
    };
  }

  if (adaptiveSpreadMode) {
    return {
      isCover: false,
      isSingle: false,
      isAdaptiveSpread: true,
      index: normalized,
      leftIndex: normalized,
      rightIndex: normalized,
      leftUrl: '',
      rightUrl: state.reader.pages[normalized].imageUrl,
      adaptiveUrl: state.reader.pages[normalized].imageUrl,
      isBlankRight: false,
      label: `Spread ${normalized} / ${Math.max(1, total - 1)}`,
      positionText: `Spread ${normalized} of ${Math.max(1, total - 1)}`
    };
  }

  let leftIndex = normalized % 2 === 0 ? normalized - 1 : normalized;
  leftIndex = Math.max(1, leftIndex);
  const rightIndex = Math.min(leftIndex + 1, max);
  return {
    isCover: false,
    isSingle: false,
    index: leftIndex,
    leftIndex,
    rightIndex,
    leftUrl: state.reader.pages[leftIndex].imageUrl,
    rightUrl: state.reader.pages[rightIndex].imageUrl,
    isBlankRight: rightIndex === leftIndex,
    label: `Pages ${leftIndex + 1}${rightIndex !== leftIndex ? `-${rightIndex + 1}` : ''} / ${total}`,
    positionText: `Pages ${leftIndex + 1}${rightIndex !== leftIndex ? `-${rightIndex + 1}` : ''} of ${total}`
  };
}

function applyReaderBookSize(spread) {
  const book = $('book');
  const stage = $('readerStage');
  if (!book || !stage || !spread) return;

  const rect = stage.getBoundingClientRect();
  if (!rect.width || !rect.height) return;

  // Keep the outer book footprint deterministic for the active display mode.
  // Two-page mode keeps a wide spread-sized frame even for the front cover, so
  // advancing from the cover into the first spread does not resize the book.
  const ratio = state.reader.displayMode === 1 ? 0.735 : 1.58;
  // Let the book use the full available reader-stage height. The stage itself is
  // already the usable area between the title bar and the bottom control rail, so
  // extra artificial padding made some scans feel vertically biased.
  const padX = document.body.classList.contains('reader-is-fullscreen') || $('readerView')?.classList.contains('fullscreen-reader') ? 8 : 12;
  const padY = 0;
  const maxWidth = Math.max(320, rect.width - padX);
  const maxHeight = Math.max(260, rect.height - padY);
  let width = Math.min(maxWidth, maxHeight * ratio);
  let height = width / ratio;
  if (height > maxHeight) {
    height = maxHeight;
    width = height * ratio;
  }

  book.style.setProperty('--guidevault-reader-width', `${Math.floor(width)}px`);
  book.style.setProperty('--guidevault-reader-height', `${Math.floor(height)}px`);
  book.classList.add('reader-sized');
}

function renderSpread(index, options = {}) {
  const spread = spreadForIndex(index);
  if (!spread) return;
  if (!options.preserveSize) applyReaderBookSize(spread);
  state.reader.index = spread.index;
  const book = $('book');
  book.classList.toggle('cover-mode', spread.isCover);
  book.classList.toggle('single-page-mode', !!spread.isSingle);
  book.classList.toggle('adaptive-spread-mode', !!spread.isAdaptiveSpread);
  updateReaderVisualModeFlags(spread);
  applyReaderShadingSettings();
  scheduleReaderPageEdgeShadingBounds();
  $('pageLeft').classList.toggle('hidden', !!spread.isSingle || !!spread.isAdaptiveSpread);
  $('pageRight').classList.remove('blank-page');

  if (spread.isSingle) {
    $('pageRightImage').src = spread.rightUrl;
    $('pageLeftImage').src = '';
    if ($('pageLabel')) $('pageLabel').textContent = spread.label;
    updateReaderOverlay(spread);
    updateReaderPageStackEffect(spread);
    scheduleReaderPageEdgeShadingBounds();
    if (state.reader.magnifierActive) { updateReaderMagnifierContent(); requestAnimationFrame(updateReaderMagnifierFromLastPointer); }
    return;
  }

  if (spread.isAdaptiveSpread) {
    $('pageLeftImage').src = '';
    $('pageRightImage').src = spread.adaptiveUrl || spread.rightUrl || '';
    $('pageRight').classList.remove('blank-page');
    if ($('pageLabel')) $('pageLabel').textContent = spread.label;
    updateReaderOverlay(spread);
    updateReaderPageStackEffect(spread);
    scheduleReaderPageEdgeShadingBounds();
    if (state.reader.magnifierActive) { updateReaderMagnifierContent(); requestAnimationFrame(updateReaderMagnifierFromLastPointer); }
    return;
  }

  $('pageLeftImage').src = spread.leftUrl;
  $('pageRightImage').src = spread.rightUrl;
  $('pageRight').classList.toggle('blank-page', spread.isBlankRight);
  if ($('pageLabel')) $('pageLabel').textContent = spread.label;
  updateReaderOverlay(spread);
  updateReaderPageStackEffect(spread);
  scheduleReaderPageEdgeShadingBounds();
  if (state.reader.magnifierActive) { updateReaderMagnifierContent(); requestAnimationFrame(updateReaderMagnifierFromLastPointer); }
}

function refreshReaderBookSize() {
  if (!$('readerView') || $('readerView').classList.contains('hidden')) return;
  const spread = spreadForIndex(state.reader.index);
  if (spread && !state.reader.animating) { applyReaderBookSize(spread); scheduleReaderPageEdgeShadingBounds(); }
}

window.addEventListener('resize', () => {
  window.clearTimeout(state.reader.resizeTimer);
  state.reader.resizeTimer = window.setTimeout(refreshReaderBookSize, 80);
});

document.addEventListener('fullscreenchange', () => {
  document.body.classList.toggle('reader-is-fullscreen', document.fullscreenElement === $('readerStage'));
  updateReaderFullscreenUi();
  window.setTimeout(refreshReaderBookSize, 80);
});

function updateReaderFullscreenUi() {
  const isReaderFullscreen = document.fullscreenElement === $('readerStage');
  const fullButton = $('readerFullscreen');
  if (fullButton) fullButton.textContent = isReaderFullscreen ? 'Exit Fullscreen' : 'Fullscreen';
  const trayExit = $('readerExitFullscreen');
  if (trayExit) trayExit.classList.toggle('hidden', !isReaderFullscreen);
}

function loadReaderBookmarks() {
  try {
    const parsed = JSON.parse(localStorage.getItem(READER_BOOKMARKS_KEY) || '{}');
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

function saveReaderBookmarks(bookmarks) {
  try { localStorage.setItem(READER_BOOKMARKS_KEY, JSON.stringify(bookmarks || {})); } catch {}
}

function readerBookmarkKey() {
  const item = state.reader.item || {};
  return String(item.id || item.filePath || item.path || item.title || '').trim();
}

function getReaderBookmark() {
  const key = readerBookmarkKey();
  if (!key) return null;
  return loadReaderBookmarks()[key] || null;
}

function currentReaderPageNumber() {
  return Math.max(1, Math.min(readerSliderPageFromSpread(spreadForIndex(state.reader.index)), readerPageCount()));
}

function bookmarkCurrentReaderPage() {
  const key = readerBookmarkKey();
  if (!key || !state.reader.item) return;
  const page = currentReaderPageNumber();
  const bookmarks = loadReaderBookmarks();
  const existing = bookmarks[key] || null;
  const isSamePage = existing && Number(existing.page) === Number(page);

  if (isSamePage) {
    delete bookmarks[key];
  } else {
    bookmarks[key] = {
      itemId: state.reader.item.id || '',
      title: state.reader.item.title || '',
      page,
      displayMode: normalizeReaderDisplayMode(state.reader.displayMode),
      savedAt: new Date().toISOString()
    };
  }

  saveReaderBookmarks(bookmarks);
  updateReaderOverlay();
}

async function exitReaderFullscreenOnly() {
  if (document.fullscreenElement) {
    try { await document.exitFullscreen(); } catch {}
  }
  updateReaderFullscreenUi();
}

async function exitReaderToLibrary() {
  setReaderOverlayVisible(false);
  await exitReaderFullscreenOnly();
  showLibraryScreen();
}

async function exitReaderToDetails() {
  const item = state.reader.item || state.selected;
  setReaderOverlayVisible(false);
  await exitReaderFullscreenOnly();
  if (item) {
    showDetailScreen(applyClientMetadataOverride(item));
  } else {
    showLibraryScreen();
  }
}

function readerPageCount() {
  return Math.max(1, state.reader.pages.length || 1);
}

function clampReaderIndex(index) {
  return Math.max(0, Math.min(Number(index) || 0, readerPageCount() - 1));
}

function readerSliderPageFromSpread(spread = null) {
  const current = spread || spreadForIndex(state.reader.index);
  if (!current) return 1;
  if (current.isSingle) return (current.index || 0) + 1;
  return (current.leftIndex ?? current.index ?? 0) + 1;
}

function updateReaderPageStackEffect(spread = null) {
  const book = $('book');
  if (!book) return;
  const total = readerPageCount();
  const current = spread || spreadForIndex(state.reader.index);
  const page = Math.max(1, Math.min(readerSliderPageFromSpread(current), total));
  const progress = total > 1 ? Math.max(0, Math.min(1, (page - 1) / (total - 1))) : 0;
  // Keep the page-stack effect tucked close to the actual scanned page edges.
  // The earlier max width was too large and made the stacks look detached from the book.
  const maxStack = 26;
  const minStack = 2;
  const visualSingle = updateReaderVisualModeFlags(current);
  if (visualSingle || current?.isSingle) {
    const right = current?.isCover && total > 1 ? Math.round(minStack + maxStack * (1 - progress)) : 0;
    book.style.setProperty('--gv-left-stack', '0px');
    book.style.setProperty('--gv-right-stack', `${right}px`);
    book.style.setProperty('--gv-left-stack-opacity', '0');
    book.style.setProperty('--gv-right-stack-opacity', right > 0 ? String(Math.min(.60, .10 + (1 - progress) * .50).toFixed(3)) : '0');
    book.style.setProperty('--gv-page-progress', String(progress.toFixed(4)));
    updateReaderPageStackGeometry();
    return;
  }
  const left = Math.round(minStack + maxStack * progress);
  const right = Math.round(minStack + maxStack * (1 - progress));
  book.style.setProperty('--gv-left-stack', `${left}px`);
  book.style.setProperty('--gv-right-stack', `${right}px`);
  book.style.setProperty('--gv-left-stack-opacity', String(Math.min(.60, .10 + progress * .50).toFixed(3)));
  book.style.setProperty('--gv-right-stack-opacity', String(Math.min(.60, .10 + (1 - progress) * .50).toFixed(3)));
  book.style.setProperty('--gv-page-progress', String(progress.toFixed(4)));
  updateReaderPageStackGeometry();
}

function transitionLabel(mode) {
  if (mode === 'fade') return 'Quick Fade';
  if (mode === 'dissolve') return 'Soft Dissolve';
  if (mode === 'slide') return 'Slide';
  if (mode === 'push') return 'Push';
  if (mode === 'page') return 'Page Turn Effect';
  return 'Stable Swap';
}

function updateReaderOverlay(spread = null) {
  const current = spread || spreadForIndex(state.reader.index);
  const total = readerPageCount();
  const sliderPage = readerSliderPageFromSpread(current);

  const position = $('readerPagePosition');
  if (position && current && !state.reader.scrubbing) position.textContent = current.positionText || current.label || '';

  const lastLabel = $('readerPageLastLabel');
  if (lastLabel) lastLabel.textContent = `Page ${total}`;

  const slider = $('readerPageSlider');
  if (slider && !state.reader.scrubbing) {
    slider.min = '1';
    slider.max = String(total);
    slider.step = '1';
    slider.value = String(Math.max(1, Math.min(sliderPage, total)));
    const pct = total <= 1 ? 0 : ((Number(slider.value) - 1) / (total - 1)) * 100;
    slider.style.setProperty('--reader-scrub-pct', `${pct}%`);
  }

  const modeSelect = $('readerDisplayModeSelect');
  if (modeSelect) modeSelect.value = String(normalizeReaderDisplayMode(state.reader.displayMode));

  const transitionSelect = $('readerTransitionSelect');
  if (transitionSelect) transitionSelect.value = state.reader.transitionMode || 'stable';

  const bookmark = getReaderBookmark();
  const bookmarkButton = $('readerBookmarkPage');
  if (bookmarkButton) {
    const samePage = bookmark && Number(bookmark.page) === Number(sliderPage);
    bookmarkButton.classList.toggle('bookmarked', !!samePage);
    bookmarkButton.setAttribute('aria-pressed', samePage ? 'true' : 'false');
    bookmarkButton.setAttribute('aria-label', samePage ? `Unbookmark Page ${bookmark.page}` : 'Bookmark current page');
    bookmarkButton.title = samePage
      ? `Unbookmark Page ${bookmark.page}`
      : (bookmark ? `Saved bookmark: Page ${bookmark.page}. Click to bookmark the current page.` : 'Bookmark current page');
  }
  updateReaderFullscreenUi();
  applyReaderZoom();
  updateReaderShadingControls();
  updateReaderAdvancedSettingsUi();
  applyReaderBackground();
  updateReaderMagnifierControls();

  // Keep compatibility with the first-pass overlay buttons in case an older HTML shell is cached.
  const modeOne = $('readerModeOne');
  const modeTwo = $('readerModeTwo');
  if (modeOne) modeOne.classList.toggle('active', state.reader.displayMode === 1);
  if (modeTwo) modeTwo.classList.toggle('active', normalizeReaderDisplayMode(state.reader.displayMode) === 2);
}

function updateReaderAdvancedSettingsUi() {
  const panel = $('readerAdvancedPanel');
  const button = $('readerAdvancedSettings');
  const open = !!state.reader.advancedVisible && !!state.reader.overlayVisible;
  if (panel) {
    panel.classList.toggle('hidden', !open);
    panel.classList.toggle('open', open);
  }
  if (button) {
    button.classList.toggle('active', open);
    button.setAttribute('aria-expanded', open ? 'true' : 'false');
  }
}

function setReaderAdvancedSettingsVisible(visible) {
  state.reader.advancedVisible = !!visible;
  if (state.reader.advancedVisible) state.reader.overlayVisible = true;
  updateReaderAdvancedSettingsUi();
  if (state.reader.overlayVisible) updateReaderOverlay();
}

function toggleReaderAdvancedSettings() {
  setReaderAdvancedSettingsVisible(!state.reader.advancedVisible);
}

function setReaderOverlayVisible(visible) {
  state.reader.overlayVisible = !!visible;
  cancelReaderLongPress();
  if (!state.reader.overlayVisible) {
    state.reader.advancedVisible = false;
    state.reader.magnifierSettingsVisible = false;
  }
  const hud = $('readerHud');
  if (hud) {
    hud.classList.toggle('hidden', !state.reader.overlayVisible);
    hud.classList.toggle('open', state.reader.overlayVisible);
  }
  const stage = $('readerStage');
  if (stage) stage.classList.toggle('reader-controls-open', state.reader.overlayVisible);
  // The bottom tray is an overlay. Do not resize/reflow the book when it opens;
  // otherwise the front cover visibly changes size just from showing controls.
  updateReaderAdvancedSettingsUi();
  if (state.reader.overlayVisible) updateReaderOverlay();
}

function toggleReaderOverlay() {
  setReaderOverlayVisible(!state.reader.overlayVisible);
}


function applyReaderZoom() {
  const book = $('book');
  const zoom = clampNumber(state.reader.zoom, 70, 145, 100);
  state.reader.zoom = zoom;
  if (book) book.style.setProperty('--reader-zoom-scale', (zoom / 100).toFixed(3));
  const zoomIn = $('readerZoomIn');
  const zoomOut = $('readerZoomOut');
  if (zoomIn) zoomIn.disabled = zoom >= 145;
  if (zoomOut) zoomOut.disabled = zoom <= 70;
}

function adjustReaderZoom(delta) {
  state.reader.zoom = clampNumber((Number(state.reader.zoom) || 100) + delta, 70, 145, 100);
  applyReaderZoom();
  setReaderOverlayVisible(true);
  scheduleReaderPageEdgeShadingBounds();
}

function setReaderDisplayMode(mode) {
  const nextMode = normalizeReaderDisplayMode(mode);
  if (normalizeReaderDisplayMode(state.reader.displayMode) === nextMode) {
    updateReaderOverlay();
    return;
  }
  state.reader.displayMode = nextMode;
  if (!state.reader.pages.length || state.reader.animating) { updateReaderOverlay(); return; }
  renderSpread(state.reader.index, { preserveSize: false });
  updateReaderOverlay();
}

function setReaderTransitionMode(mode) {
  state.reader.transitionMode = normalizeReaderTransitionMode(mode);
  updateReaderOverlay();
}

function setReaderSliderPreview(pageNumber) {
  const total = readerPageCount();
  const page = Math.max(1, Math.min(Number(pageNumber) || 1, total));
  const slider = $('readerPageSlider');
  if (slider) {
    const pct = total <= 1 ? 0 : ((page - 1) / (total - 1)) * 100;
    slider.style.setProperty('--reader-scrub-pct', `${pct}%`);
  }
  const position = $('readerPagePosition');
  if (position) position.textContent = `Jump to page ${page} of ${total}`;
}

function jumpReaderToPage(pageNumber) {
  if (!state.reader.pages.length || state.reader.animating) return;
  const page = Math.max(1, Math.min(Number(pageNumber) || 1, readerPageCount()));
  renderSpread(page - 1, { preserveSize: false });
  setReaderOverlayVisible(true);
}

function wait(ms) {
  return new Promise(resolve => window.setTimeout(resolve, ms));
}

function waitForReaderPaint() {
  return new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
}

function clearReaderTransitionStyles(shell) {
  if (!shell) return;
  shell.classList.remove(
    'reader-fade-out', 'reader-fade-in',
    'reader-slide-out-left', 'reader-slide-out-right',
    'reader-slide-in-left', 'reader-slide-in-right'
  );
  ['transition','opacity','transform','will-change'].forEach(name => shell.style.removeProperty(name));
}

async function animateReaderShell(shell, styles, duration) {
  if (!shell) return;
  Object.entries(styles).forEach(([name, value]) => shell.style.setProperty(name, value, 'important'));
  await wait(duration);
}


function readerPageTurnSource(dir) {
  const book = $('book');
  const single = book?.classList.contains('single-page-mode') || book?.classList.contains('cover-mode');
  const spread = spreadForIndex(state.reader.index);
  if (spread?.isAdaptiveSpread) {
    const pageIndex = spread?.rightIndex ?? state.reader.index;
    return { page: $('pageRight'), image: $('pageRightImage'), side: 'right', pageIndex, isAdaptiveSpread: true };
  }
  if (single || spread?.isSingle) {
    const pageIndex = spread?.rightIndex ?? state.reader.index;
    return { page: $('pageRight'), image: $('pageRightImage'), side: 'right', pageIndex };
  }
  if (dir === 'prev') return { page: $('pageLeft'), image: $('pageLeftImage'), side: 'left', pageIndex: spread?.leftIndex ?? state.reader.index };
  return { page: $('pageRight'), image: $('pageRightImage'), side: 'right', pageIndex: spread?.rightIndex ?? state.reader.index };
}

function readerPageUrlAt(index) {
  const i = Number(index);
  if (!Number.isFinite(i) || i < 0 || i >= state.reader.pages.length) return '';
  return state.reader.pages[i]?.imageUrl || '';
}

function readerPageTurnBackUrl(nextIndex, dir, source) {
  // Physical backside of the currently turning sheet.
  // Example: if spread is pages 14-15 and page 15 is turning forward,
  // the backside must be page 16, not page 15 mirrored.
  const sourceIndex = Number(source?.pageIndex);
  if (Number.isFinite(sourceIndex)) {
    const backsideIndex = dir === 'prev' ? sourceIndex - 1 : sourceIndex + 1;
    const backsideUrl = readerPageUrlAt(backsideIndex);
    if (backsideUrl) return backsideUrl;
  }

  const nextSpread = spreadForIndex(nextIndex);
  if (!nextSpread) return '';
  if (nextSpread.isSingle) return nextSpread.rightUrl || nextSpread.leftUrl || '';
  return dir === 'prev'
    ? (nextSpread.rightUrl || nextSpread.leftUrl || '')
    : (nextSpread.leftUrl || nextSpread.rightUrl || '');
}

function readerPageTurnUnderlayUrl(nextIndex, dir, source = null) {
  // Page revealed behind the source page as it lifts away.
  // Forward from pages 14-15 should reveal page 17 on the right while page 16 folds to the left.
  const sourceIndex = Number(source?.pageIndex);
  if (Number.isFinite(sourceIndex)) {
    const underlayIndex = dir === 'prev' ? sourceIndex - 2 : sourceIndex + 2;
    const url = readerPageUrlAt(underlayIndex);
    if (url) return url;
  }

  const nextSpread = spreadForIndex(nextIndex);
  if (!nextSpread) return '';
  if (nextSpread.isSingle) return nextSpread.rightUrl || nextSpread.leftUrl || '';
  return dir === 'prev'
    ? (nextSpread.leftUrl || nextSpread.rightUrl || '')
    : (nextSpread.rightUrl || nextSpread.leftUrl || '');
}

function applyReaderTurnGeometry(el, geometry) {
  if (!el || !geometry) return;
  const px = value => `${(Number(value) || 0).toFixed(2)}px`;
  el.style.left = px(geometry.left);
  el.style.top = px(geometry.top);
  el.style.width = px(geometry.width);
  el.style.height = px(geometry.height);
}

function lockExperimentalTurnFootprint(book, shell) {
  if (!book || !shell) return;
  // Do not measure with getBoundingClientRect() here: it includes the reader zoom
  // transform, which made Page Turn Experimental briefly shrink/misalign the book.
  // The reader-sized CSS variables already hold the stable unscaled footprint.
  const computed = getComputedStyle(book);
  const currentWidth = computed.getPropertyValue('--guidevault-reader-width').trim();
  const currentHeight = computed.getPropertyValue('--guidevault-reader-height').trim();
  if (!currentWidth || !currentHeight) {
    const w = book.offsetWidth || shell.offsetWidth || 0;
    const h = book.offsetHeight || shell.offsetHeight || 0;
    if (w && h) {
      book.style.setProperty('--guidevault-reader-width', `${w.toFixed(2)}px`);
      book.style.setProperty('--guidevault-reader-height', `${h.toFixed(2)}px`);
    }
  }
  const shellW = shell.offsetWidth || 0;
  const shellH = shell.offsetHeight || 0;
  if (shellW && shellH) {
    shell.style.setProperty('--guidevault-turn-shell-width', `${shellW.toFixed(2)}px`);
    shell.style.setProperty('--guidevault-turn-shell-height', `${shellH.toFixed(2)}px`);
  }
  book.classList.add('reader-page-turn-footprint-locked');
  shell.classList.add('reader-page-turn-shell-locked');
}

function unlockExperimentalTurnFootprint(book, shell) {
  if (book) book.classList.remove('reader-page-turn-footprint-locked');
  if (shell) shell.classList.remove('reader-page-turn-shell-locked');
}

function readerPageGeometry(pageEl, imgEl, side, shell) {
  if (!pageEl || !imgEl || !shell) return null;
  const pageW = pageEl.clientWidth || pageEl.offsetWidth || 0;
  const pageH = pageEl.clientHeight || pageEl.offsetHeight || 0;
  const bounds = pageImageContainBounds(pageEl, imgEl, side) || {
    left: 0,
    right: pageW,
    top: 0,
    height: pageH
  };
  // pageLeft/pageRight are direct children of pageShell; offsetLeft/Top are layout
  // coordinates and stay aligned when the whole book is zoomed with CSS transform.
  return {
    width: Math.max(8, bounds.right - bounds.left),
    height: Math.max(8, bounds.height),
    left: (pageEl.offsetLeft || 0) + bounds.left,
    top: (pageEl.offsetTop || 0) + bounds.top
  };
}


function readerCenteredContainGeometry(containerEl, imgEl) {
  if (!containerEl || !imgEl) return null;
  const containerWidth = containerEl.clientWidth || containerEl.offsetWidth || 0;
  const containerHeight = containerEl.clientHeight || containerEl.offsetHeight || 0;
  if (!containerWidth || !containerHeight) return null;
  const naturalW = imgEl.naturalWidth || 0;
  const naturalH = imgEl.naturalHeight || 0;
  const ratio = naturalW > 0 && naturalH > 0 ? naturalW / naturalH : 0.735;
  let imageW = containerWidth;
  let imageH = imageW / ratio;
  if (imageH > containerHeight) {
    imageH = containerHeight;
    imageW = imageH * ratio;
  }
  return {
    left: (containerEl.offsetLeft || 0) + Math.max(0, (containerWidth - imageW) / 2),
    top: (containerEl.offsetTop || 0) + Math.max(0, (containerHeight - imageH) / 2),
    width: Math.max(8, imageW),
    height: Math.max(8, imageH)
  };
}

function readerOppositePageGeometry(dir, shell) {
  const side = dir === 'prev' ? 'right' : 'left';
  const page = side === 'left' ? $('pageLeft') : $('pageRight');
  const image = side === 'left' ? $('pageLeftImage') : $('pageRightImage');
  return readerPageGeometry(page, image, side, shell);
}

function makeReaderTurnImageLayer(className, src, geometry, origin) {
  const layer = document.createElement('div');
  layer.className = className;
  applyReaderTurnGeometry(layer, geometry);
  layer.style.transformOrigin = origin;
  const img = document.createElement('img');
  img.alt = '';
  img.src = src || '';
  layer.appendChild(img);
  return layer;
}

function readerCurlTurnGeometry(fullGeometry, dir, role) {
  if (!fullGeometry) return null;
  // Use the full page image footprint for the turning sheet. Earlier paper-strip
  // attempts cropped too aggressively, making the animated page look smaller
  // than the real spread. We still use lighting/rotation to suggest paper curl,
  // but the visual page now starts from the full scanned page size.
  const ratio = 1.0;
  const width = Math.max(24, fullGeometry.width * ratio);
  let left = fullGeometry.left;
  const nearRightSpine = (dir === 'prev' && role === 'front') || (dir !== 'prev' && role === 'back');
  const nearLeftSpine = (dir !== 'prev' && role === 'front') || (dir === 'prev' && role === 'back');
  if (nearRightSpine) left = fullGeometry.left + fullGeometry.width - width;
  if (nearLeftSpine) left = fullGeometry.left;
  return {
    left,
    top: fullGeometry.top,
    width,
    height: fullGeometry.height,
    cropLeft: left - fullGeometry.left,
    cropTop: 0,
    fullWidth: fullGeometry.width,
    fullHeight: fullGeometry.height
  };
}

function makeReaderTurnImageCropLayer(className, src, cropGeometry, fullGeometry, origin) {
  const layer = document.createElement('div');
  layer.className = `${className} reader-page-turn-curl-strip`;
  applyReaderTurnGeometry(layer, cropGeometry || fullGeometry);
  layer.style.transformOrigin = origin;
  const img = document.createElement('img');
  img.alt = '';
  img.src = src || '';
  img.style.position = 'absolute';
  img.style.left = `${-(cropGeometry?.cropLeft || 0)}px`;
  img.style.top = `${-(cropGeometry?.cropTop || 0)}px`;
  img.style.width = `${Math.max(8, fullGeometry?.width || cropGeometry?.width || 0)}px`;
  img.style.height = `${Math.max(8, fullGeometry?.height || cropGeometry?.height || 0)}px`;
  img.style.maxWidth = 'none';
  img.style.maxHeight = 'none';
  img.style.objectFit = 'fill';
  img.style.display = 'block';
  layer.appendChild(img);
  return layer;
}

function makeReaderTurnShadowLayer(className, geometry, origin = 'center center') {
  if (!geometry) return null;
  const layer = document.createElement('div');
  layer.className = className;
  applyReaderTurnGeometry(layer, geometry);
  layer.style.transformOrigin = origin;
  return layer;
}

function readerSpineTurnGeometry(shell) {
  if (!shell) return null;
  const shellW = shell.clientWidth || shell.offsetWidth || 0;
  const shellH = shell.clientHeight || shell.offsetHeight || 0;
  if (!shellW || !shellH) return null;
  const width = Math.max(52, Math.min(138, shellW * 0.095));
  return {
    left: (shellW / 2) - (width / 2),
    top: 0,
    width,
    height: shellH
  };
}

function makeReaderPageTurnLayer(dir, nextIndex) {
  const shell = $('pageShell');
  const source = readerPageTurnSource(dir);
  if (!shell || !source.page || !source.image || !source.image.src) return null;

  const sourceGeometry = readerPageGeometry(source.page, source.image, source.side, shell);
  if (!sourceGeometry) return null;

  const destinationGeometry = readerOppositePageGeometry(dir, shell) || sourceGeometry;
  const frontGeometry = readerCurlTurnGeometry(sourceGeometry, dir, 'front') || sourceGeometry;
  const backGeometry = readerCurlTurnGeometry(destinationGeometry, dir, 'back') || destinationGeometry;
  const underlayUrl = readerPageTurnUnderlayUrl(nextIndex, dir, source);
  const backUrl = readerPageTurnBackUrl(nextIndex, dir, source) || underlayUrl || source.image.src;

  let underlay = null;
  if (underlayUrl) {
    underlay = makeReaderTurnImageLayer(
      `reader-page-turn-underlay ${dir === 'prev' ? 'turn-prev' : 'turn-next'}`,
      underlayUrl,
      sourceGeometry,
      'center center'
    );
    shell.appendChild(underlay);
  }

  const frontOrigin = dir === 'prev' ? 'right center' : 'left center';
  const backOrigin = dir === 'prev' ? 'left center' : 'right center';

  // The animated sheet now uses cropped near-spine strips from the real page
  // images instead of squeezing a full page into a narrow animated layer.
  // That keeps the live spread stable and makes the flip feel more like paper.
  const front = makeReaderTurnImageCropLayer(
    `reader-page-turn-flat-layer reader-page-turn-front-sheet ${dir === 'prev' ? 'turn-prev' : 'turn-next'}`,
    source.image.src,
    frontGeometry,
    sourceGeometry,
    frontOrigin
  );
  const back = makeReaderTurnImageCropLayer(
    `reader-page-turn-flat-layer reader-page-turn-back-sheet ${dir === 'prev' ? 'turn-prev' : 'turn-next'}`,
    backUrl,
    backGeometry,
    destinationGeometry,
    backOrigin
  );
  back.classList.add('hidden');

  front.dataset.frontPageIndex = String(source.pageIndex ?? '');
  front.dataset.backPageIndex = String(Number.isFinite(Number(source.pageIndex)) ? (dir === 'prev' ? Number(source.pageIndex) - 1 : Number(source.pageIndex) + 1) : '');
  back.dataset.backPageIndex = front.dataset.backPageIndex || '';

  const sourceShadow = makeReaderTurnShadowLayer(
    `reader-page-turn-dynamic-shadow reader-page-turn-source-shadow ${dir === 'prev' ? 'turn-prev' : 'turn-next'}`,
    sourceGeometry,
    frontOrigin
  );
  const destinationShadow = makeReaderTurnShadowLayer(
    `reader-page-turn-dynamic-shadow reader-page-turn-destination-shadow ${dir === 'prev' ? 'turn-prev' : 'turn-next'}`,
    destinationGeometry,
    backOrigin
  );
  const spineShadow = makeReaderTurnShadowLayer(
    `reader-page-turn-spine-shadow ${dir === 'prev' ? 'turn-prev' : 'turn-next'}`,
    readerSpineTurnGeometry(shell),
    'center center'
  );

  if (sourceShadow) shell.appendChild(sourceShadow);
  if (destinationShadow) shell.appendChild(destinationShadow);
  if (spineShadow) shell.appendChild(spineShadow);
  shell.appendChild(front);
  shell.appendChild(back);
  return { front, back, underlay, sourceShadow, destinationShadow, spineShadow };
}

function makeReaderMeshStripLayer({ className, src, fullGeometry, stripIndex, stripCount, role, dir }) {
  const stripWidth = fullGeometry.width / stripCount;
  // Slightly wider overlap avoids visible seams/gaps when the strips bend.
  const overlap = Math.max(3.0, stripWidth * 0.085);
  const left = fullGeometry.left + (stripIndex * stripWidth) - (stripIndex > 0 ? overlap * 0.5 : 0);
  const width = stripWidth + (stripIndex > 0 ? overlap * 0.5 : 0) + (stripIndex < stripCount - 1 ? overlap * 0.5 : 0);
  const layer = document.createElement('div');
  layer.className = `${className} reader-page-turn-mesh-strip reader-page-turn-mesh-${role} ${dir === 'prev' ? 'turn-prev' : 'turn-next'}`;
  layer.style.left = `${left.toFixed(2)}px`;
  layer.style.top = `${fullGeometry.top.toFixed(2)}px`;
  layer.style.width = `${Math.max(2, width).toFixed(2)}px`;
  layer.style.height = `${fullGeometry.height.toFixed(2)}px`;
  layer.style.zIndex = String(role === 'back' ? 58 + stripIndex : 54 + stripIndex);

  const img = document.createElement('img');
  img.alt = '';
  img.src = src || '';
  img.style.position = 'absolute';
  img.style.left = `${(-(stripIndex * stripWidth) + (stripIndex > 0 ? overlap * 0.5 : 0)).toFixed(2)}px`;
  img.style.top = '0px';
  img.style.width = `${fullGeometry.width.toFixed(2)}px`;
  img.style.height = `${fullGeometry.height.toFixed(2)}px`;
  img.style.maxWidth = 'none';
  img.style.maxHeight = 'none';
  img.style.objectFit = 'fill';
  img.style.display = 'block';
  layer.appendChild(img);
  return { layer, stripWidth, left, width };
}

function readerMeshStripInitialTransform(role, dir, i, stripCount, stripWidth) {
  // AE-style mesh-curl pass:
  // keep the curled page visually larger by not collapsing every strip all the way
  // into the spine. The strip nearest the binding remains pinned while the outer
  // strips travel in an arc-like stagger.
  // Keep the curl page much closer to real page scale. The previous mesh pass
  // collapsed strips too aggressively, so the turning page looked undersized.
  const collapse = 0.24;
  const rotate = 68;
  const minScale = 0.74;
  const moveToSpineNext = -i * stripWidth * collapse;
  const moveToSpinePrev = (stripCount - 1 - i) * stripWidth * collapse;
  const moveFromSpineNext = (stripCount - 1 - i) * stripWidth * collapse;
  const moveFromSpinePrev = -i * stripWidth * collapse;
  if (role === 'front') {
    return dir === 'prev'
      ? `translate3d(${moveToSpinePrev.toFixed(2)}px,0,0) perspective(2400px) rotateY(${rotate}deg) scaleX(${minScale})`
      : `translate3d(${moveToSpineNext.toFixed(2)}px,0,0) perspective(2400px) rotateY(${-rotate}deg) scaleX(${minScale})`;
  }
  return dir === 'prev'
    ? `translate3d(${moveFromSpinePrev.toFixed(2)}px,0,0) perspective(2400px) rotateY(${-rotate}deg) scaleX(${minScale})`
    : `translate3d(${moveFromSpineNext.toFixed(2)}px,0,0) perspective(2400px) rotateY(${rotate}deg) scaleX(${minScale})`;
}

function readerMeshStripDelay(role, dir, i, stripCount) {
  // A smaller stagger with earlier backside overlap removes the "tiny strip" feel
  // and makes the strips read as one bending sheet rather than separate tiles.
  const stagger = 8;
  const wave = dir === 'prev' ? i : (stripCount - 1 - i);
  if (role === 'front') {
    // Free edge starts first; spine-side strips follow.
    return wave * stagger;
  }
  // Backside begins before the front completely reaches vertical, like a paper curl.
  const base = 132;
  return base + wave * stagger;
}


function readerCanUseWebGlPageCurl() {
  try {
    const canvas = document.createElement('canvas');
    return !!(canvas.getContext('webgl', { alpha: true, antialias: true }) || canvas.getContext('experimental-webgl', { alpha: true, antialias: true }));
  } catch {
    return false;
  }
}

function readerLoadImageForTexture(url) {
  if (!url) return Promise.reject(new Error('Missing image URL'));
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.decoding = 'async';
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error('Failed to load page texture'));
    img.src = url;
  });
}

function readerCreateWebGlShader(gl, type, source) {
  const shader = gl.createShader(type);
  gl.shaderSource(shader, source);
  gl.compileShader(shader);
  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    const message = gl.getShaderInfoLog(shader) || 'shader compile failed';
    gl.deleteShader(shader);
    throw new Error(message);
  }
  return shader;
}

function readerCreateWebGlProgram(gl) {
  const vertex = readerCreateWebGlShader(gl, gl.VERTEX_SHADER, `
    attribute vec2 a_position;
    attribute vec2 a_texCoord;
    varying vec2 v_texCoord;
    void main() {
      gl_Position = vec4(a_position, 0.0, 1.0);
      v_texCoord = a_texCoord;
    }
  `);
  const fragment = readerCreateWebGlShader(gl, gl.FRAGMENT_SHADER, `
    precision mediump float;
    uniform sampler2D u_texture;
    uniform float u_alpha;
    uniform float u_shadow;
    varying vec2 v_texCoord;
    void main() {
      vec4 color = texture2D(u_texture, v_texCoord);
      float edge = min(v_texCoord.x, 1.0 - v_texCoord.x);
      float fold = 1.0 - smoothstep(0.0, 0.22, edge);
      float center = smoothstep(0.15, 0.55, abs(v_texCoord.x - 0.5));
      float shade = 1.0 - (fold * 0.22 + center * 0.08) * u_shadow;
      gl_FragColor = vec4(color.rgb * shade, color.a * u_alpha);
    }
  `);
  const program = gl.createProgram();
  gl.attachShader(program, vertex);
  gl.attachShader(program, fragment);
  gl.linkProgram(program);
  gl.deleteShader(vertex);
  gl.deleteShader(fragment);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
    const message = gl.getProgramInfoLog(program) || 'program link failed';
    gl.deleteProgram(program);
    throw new Error(message);
  }
  return program;
}

function readerTextureSourceForWebGl(gl, img) {
  const maxSize = gl.getParameter(gl.MAX_TEXTURE_SIZE) || 4096;
  if (img.naturalWidth <= maxSize && img.naturalHeight <= maxSize) return img;
  const scale = Math.min(maxSize / img.naturalWidth, maxSize / img.naturalHeight, 1);
  const canvas = document.createElement('canvas');
  canvas.width = Math.max(1, Math.floor(img.naturalWidth * scale));
  canvas.height = Math.max(1, Math.floor(img.naturalHeight * scale));
  const ctx = canvas.getContext('2d');
  ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
  return canvas;
}

function readerCreateWebGlTexture(gl, img) {
  const texture = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, texture);
  // Keep texture coordinates in the same top-to-bottom orientation as the reader DOM.
  // Flipping here made the WebGL backside briefly render upside down during the curl.
  gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, readerTextureSourceForWebGl(gl, img));
  gl.bindTexture(gl.TEXTURE_2D, null);
  return texture;
}

function readerClipPoint(x, y, shellWidth, shellHeight) {
  return [(x / shellWidth) * 2 - 1, 1 - (y / shellHeight) * 2];
}

function readerBuildWebGlQuad(geometry, shellWidth, shellHeight, flipU = false) {
  const x0 = geometry.left;
  const x1 = geometry.left + geometry.width;
  const y0 = geometry.top;
  const y1 = geometry.top + geometry.height;
  const p00 = readerClipPoint(x0, y0, shellWidth, shellHeight);
  const p10 = readerClipPoint(x1, y0, shellWidth, shellHeight);
  const p01 = readerClipPoint(x0, y1, shellWidth, shellHeight);
  const p11 = readerClipPoint(x1, y1, shellWidth, shellHeight);
  const u0 = flipU ? 1 : 0;
  const u1 = flipU ? 0 : 1;
  return {
    positions: new Float32Array([
      p00[0], p00[1], p10[0], p10[1], p01[0], p01[1],
      p01[0], p01[1], p10[0], p10[1], p11[0], p11[1]
    ]),
    texCoords: new Float32Array([
      u0,0, u1,0, u0,1,
      u0,1, u1,0, u1,1
    ])
  };
}

function readerBuildWebGlCurlMesh({ geometry, dir, progress, shellWidth, shellHeight, backside, texRange = null }) {
  const cols = 34;
  const rows = 5;
  const positions = [];
  const texCoords = [];
  const initialSign = dir === 'prev' ? -1 : 1;
  const width = geometry.width;
  const height = geometry.height;
  const spine = dir === 'prev' ? geometry.left + width : geometry.left;
  const yTop = geometry.top;
  const faceProgress = Math.max(0, Math.min(1, progress));
  const curlAmount = Math.sin(Math.PI * faceProgress);
  // Slightly stronger WebGL arc now that this renderer is the best curl path.
  // Keep it conservative enough to avoid the old detached-page look.
  const bend = width * 0.42 * curlAmount;
  const fan = height * 0.045 * curlAmount;

  const makePoint = (u, v) => {
    // u is measured from the binding/spine edge to the free page edge.
    // At p=0 the sheet is on its source side; at p=1 it is on the opposite side.
    const sideBlend = initialSign * (1 - 2 * faceProgress);
    const arc = Math.sin(Math.PI * u) * bend * initialSign;
    const edgeLag = Math.pow(u, 1.55) * width * 0.045 * Math.sin(Math.PI * faceProgress) * initialSign;
    const x = spine + (u * width * sideBlend) + arc + edgeLag;
    const y = yTop + (v * height) + ((v - 0.5) * fan * Math.sin(Math.PI * u));
    return readerClipPoint(x, y, shellWidth, shellHeight);
  };

  const texUFor = u => {
    const base = !backside
      ? (dir === 'prev' ? 1 - u : u)
      : (dir === 'prev' ? u : 1 - u);
    if (!Array.isArray(texRange) || texRange.length < 2) return base;
    const minU = Math.max(0, Math.min(1, Number(texRange[0])));
    const maxU = Math.max(0, Math.min(1, Number(texRange[1])));
    return minU + (maxU - minU) * base;
  };

  for (let row = 0; row < rows; row += 1) {
    const v0 = row / rows;
    const v1 = (row + 1) / rows;
    for (let col = 0; col < cols; col += 1) {
      const u0 = col / cols;
      const u1 = (col + 1) / cols;
      const a = makePoint(u0, v0);
      const b = makePoint(u1, v0);
      const c = makePoint(u0, v1);
      const d = makePoint(u1, v1);
      positions.push(a[0], a[1], b[0], b[1], c[0], c[1], c[0], c[1], b[0], b[1], d[0], d[1]);
      texCoords.push(texUFor(u0), v0, texUFor(u1), v0, texUFor(u0), v1, texUFor(u0), v1, texUFor(u1), v0, texUFor(u1), v1);
    }
  }

  return { positions: new Float32Array(positions), texCoords: new Float32Array(texCoords) };
}

function readerDrawWebGlMesh(gl, program, buffers, texture, mesh, alpha = 1, shadow = 1) {
  if (!mesh || !mesh.positions?.length || !mesh.texCoords?.length) return;
  gl.useProgram(program);
  gl.activeTexture(gl.TEXTURE0);
  gl.bindTexture(gl.TEXTURE_2D, texture);
  gl.uniform1i(buffers.uTexture, 0);
  gl.uniform1f(buffers.uAlpha, alpha);
  gl.uniform1f(buffers.uShadow, shadow);

  gl.bindBuffer(gl.ARRAY_BUFFER, buffers.position);
  gl.bufferData(gl.ARRAY_BUFFER, mesh.positions, gl.DYNAMIC_DRAW);
  gl.enableVertexAttribArray(buffers.aPosition);
  gl.vertexAttribPointer(buffers.aPosition, 2, gl.FLOAT, false, 0, 0);

  gl.bindBuffer(gl.ARRAY_BUFFER, buffers.texCoord);
  gl.bufferData(gl.ARRAY_BUFFER, mesh.texCoords, gl.DYNAMIC_DRAW);
  gl.enableVertexAttribArray(buffers.aTexCoord);
  gl.vertexAttribPointer(buffers.aTexCoord, 2, gl.FLOAT, false, 0, 0);

  gl.drawArrays(gl.TRIANGLES, 0, mesh.positions.length / 2);
}

function readerCreateWebGlPageCurlCanvas(shell) {
  const canvas = document.createElement('canvas');
  canvas.className = 'reader-webgl-page-curl';
  canvas.setAttribute('aria-hidden', 'true');
  const width = Math.max(1, shell.clientWidth || shell.offsetWidth || 1);
  const height = Math.max(1, shell.clientHeight || shell.offsetHeight || 1);
  const dpr = Math.max(1, Math.min(window.devicePixelRatio || 1, 2));
  canvas.style.width = `${width}px`;
  canvas.style.height = `${height}px`;
  canvas.width = Math.floor(width * dpr);
  canvas.height = Math.floor(height * dpr);
  shell.appendChild(canvas);
  const gl = canvas.getContext('webgl', { alpha: true, antialias: true, premultipliedAlpha: true })
    || canvas.getContext('experimental-webgl', { alpha: true, antialias: true, premultipliedAlpha: true });
  if (!gl) {
    canvas.remove();
    return null;
  }
  gl.viewport(0, 0, canvas.width, canvas.height);
  gl.enable(gl.BLEND);
  gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
  gl.clearColor(0, 0, 0, 0);
  return { canvas, gl, width, height };
}


async function performReaderCoverOpenTransition(nextIndex) {
  const shell = $('pageShell');
  const book = $('book');
  const coverImage = $('pageRightImage');
  const leftImage = $('pageLeftImage');
  if (!shell || !book || !coverImage?.src) {
    renderSpread(nextIndex, { preserveSize: true });
    return;
  }

  const toSpread = spreadForIndex(nextIndex);
  const pageTwoUrl = toSpread?.leftUrl || readerPageUrlAt(1) || '';
  const pageThreeUrl = toSpread?.rightUrl || readerPageUrlAt(2) || pageTwoUrl || '';
  const coverUrl = coverImage.src;
  let overlay = null;
  let webgl = null;
  let textures = [];
  const previousCoverVisibility = coverImage.style.visibility || '';
  const previousLeftVisibility = leftImage?.style?.visibility || '';

  try {
    // v1.5.0: keep the good cover slide/curl, but make the animation use the
    // same hidden destination spread geometry that the reader will use after the
    // handoff. That keeps the page-3 corner and the right-side page stack from
    // sitting in one place during WebGL and then snapping to another place when
    // the real DOM spread is revealed.
    const [coverImg, pageTwoImg, pageThreeImg] = await Promise.all([
      readerLoadImageForTexture(coverUrl),
      readerLoadImageForTexture(pageTwoUrl || coverUrl),
      readerLoadImageForTexture(pageThreeUrl || pageTwoUrl || coverUrl)
    ]);

    const containInRect = (rect, naturalW, naturalH, objectPosition = 'center') => {
      const ratio = naturalW > 0 && naturalH > 0 ? naturalW / naturalH : 0.735;
      let width = rect.width;
      let height = width / ratio;
      if (height > rect.height) {
        height = rect.height;
        width = height * ratio;
      }
      const extraX = Math.max(0, rect.width - width);
      const leftOffset = objectPosition === 'left' ? 0 : objectPosition === 'right' ? extraX : extraX / 2;
      return {
        left: rect.left + leftOffset,
        top: rect.top + Math.max(0, (rect.height - height) / 2),
        width: Math.max(8, width),
        height: Math.max(8, height)
      };
    };

    const geometryFromPageAndImage = (pageEl, imageInfo, side) => {
      if (!pageEl || !imageInfo) return null;
      const pageW = pageEl.clientWidth || pageEl.offsetWidth || 0;
      const pageH = pageEl.clientHeight || pageEl.offsetHeight || 0;
      if (!pageW || !pageH) return null;
      const naturalW = imageInfo.naturalWidth || imageInfo.width || 0;
      const naturalH = imageInfo.naturalHeight || imageInfo.height || 0;
      const ratio = naturalW > 0 && naturalH > 0 ? naturalW / naturalH : 0.735;
      let imageW = pageW;
      let imageH = imageW / ratio;
      if (imageH > pageH) {
        imageH = pageH;
        imageW = imageH * ratio;
      }
      const extraX = Math.max(0, pageW - imageW);
      const x = side === 'left' ? extraX : side === 'right' ? 0 : extraX / 2;
      return {
        left: (pageEl.offsetLeft || 0) + x,
        top: (pageEl.offsetTop || 0) + Math.max(0, (pageH - imageH) / 2),
        width: Math.max(8, imageW),
        height: Math.max(8, imageH)
      };
    };

    const mixGeometry = (a, b, t) => {
      if (!a || !b) return a || b;
      const p = Math.max(0, Math.min(1, Number(t) || 0));
      return {
        left: a.left + (b.left - a.left) * p,
        top: a.top + (b.top - a.top) * p,
        width: a.width + (b.width - a.width) * p,
        height: a.height + (b.height - a.height) * p
      };
    };

    // Measure the currently visible centered cover before the reader is switched
    // underneath to the hidden two-page destination spread.
    const currentRightPage = $('pageRight');
    const currentPageRect = currentRightPage ? {
      left: (shell.offsetLeft || 0) + (currentRightPage.offsetLeft || 0),
      top: (shell.offsetTop || 0) + (currentRightPage.offsetTop || 0),
      width: currentRightPage.clientWidth || currentRightPage.offsetWidth || Math.max(1, (book.offsetWidth || 1) / 2),
      height: currentRightPage.clientHeight || currentRightPage.offsetHeight || Math.max(1, book.offsetHeight || 1)
    } : {
      left: Math.max(0, (book.offsetWidth || 1) * .25),
      top: 0,
      width: Math.max(1, (book.offsetWidth || 1) * .5),
      height: Math.max(1, book.offsetHeight || 1)
    };
    const fromGeometry = containInRect(currentPageRect, coverImg.naturalWidth || 0, coverImg.naturalHeight || 0, 'center');

    overlay = document.createElement('div');
    overlay.className = 'reader-cover-open-overlay reader-cover-open-webgl-slide';
    applyReaderTurnGeometry(overlay, fromGeometry);
    overlay.style.transform = 'translate3d(0,0,0) scale(1,1)';
    overlay.style.transformOrigin = 'left top';
    const overlayImg = document.createElement('img');
    overlayImg.alt = '';
    overlayImg.src = coverUrl;
    overlay.appendChild(overlayImg);
    book.appendChild(overlay);

    book.classList.add('reader-cover-open-active', 'reader-cover-stack-hidden', 'reader-cover-opening-stage');
    shell.classList.add('reader-cover-open-slide-phase');
    if (coverImage?.style) coverImage.style.visibility = 'hidden';
    if (leftImage?.style) leftImage.style.visibility = 'hidden';

    // Paint the destination spread now, under the overlay and hidden from view.
    // This lets all WebGL reveal geometry be measured from the final layout instead
    // of guessing from the centered cover layout.
    renderSpread(nextIndex, { preserveSize: true });
    if (coverImage?.style) coverImage.style.visibility = 'hidden';
    if (leftImage?.style) leftImage.style.visibility = 'hidden';
    lockExperimentalTurnFootprint(book, shell);
    shell.classList.add('reader-page-turn-active', 'reader-page-turn-webgl-active', 'reader-cover-webgl-open-active');
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
    await waitForReaderPaint();
    await waitForReaderPaint();

    const pageLeft = $('pageLeft');
    const pageRight = $('pageRight');
    const pageTwoGeometry = geometryFromPageAndImage(pageLeft, pageTwoImg, 'left');
    const pageThreeGeometry = geometryFromPageAndImage(pageRight, pageThreeImg, 'right');
    const coverTargetGeometry = geometryFromPageAndImage(pageRight, coverImg, 'right') || pageThreeGeometry || fromGeometry;
    const pageTwoCurlTargetGeometry = pageTwoGeometry
      ? { left: pageTwoGeometry.left + pageTwoGeometry.width, top: pageTwoGeometry.top, width: pageTwoGeometry.width, height: pageTwoGeometry.height }
      : coverTargetGeometry;

    let pageThreeStackRevealed = false;
    const revealPageThreeStack = () => {
      if (pageThreeStackRevealed) return;
      pageThreeStackRevealed = true;
      updateReaderPageStackEffect(spreadForIndex(nextIndex));
      updateReaderPageEdgeShadingBounds();
      book.classList.add('reader-cover-page3-stack-visible');
    };

    const moveX = coverTargetGeometry.left - fromGeometry.left;
    const moveY = coverTargetGeometry.top - fromGeometry.top;
    const scaleX = coverTargetGeometry.width / Math.max(1, fromGeometry.width);
    const scaleY = coverTargetGeometry.height / Math.max(1, fromGeometry.height);
    const transformAt = (k) => {
      const sx = 1 + (scaleX - 1) * k;
      const sy = 1 + (scaleY - 1) * k;
      return `translate3d(${(moveX * k).toFixed(2)}px,${(moveY * k).toFixed(2)}px,0) scale(${sx.toFixed(5)},${sy.toFixed(5)})`;
    };
    const landedTransform = transformAt(1);

    // Stage 1: the part that tested well — a pure, smooth slide from the centered
    // cover into the final right-page slot. The scale term is normally tiny, but it
    // absorbs small cover-vs-spread padding differences instead of letting them pop.
    await overlay.animate([
      { transform: transformAt(0), opacity: 1, filter: 'brightness(1)', offset: 0 },
      { transform: transformAt(.26), opacity: 1, filter: 'brightness(1)', offset: .30 },
      { transform: transformAt(.72), opacity: 1, filter: 'brightness(1)', offset: .74 },
      { transform: landedTransform, opacity: 1, filter: 'brightness(1)', offset: 1 }
    ], { duration: 1060, easing: 'cubic-bezier(.20,.74,.16,1)', fill: 'forwards' }).finished.catch(() => {});

    shell.classList.remove('reader-cover-open-slide-phase');

    // Stage 2: WebGL cover curl. Page 3 is drawn at its exact final DOM footprint,
    // while the cover/page-2 sheet curls from the right page toward the measured left page.
    webgl = readerCreateWebGlPageCurlCanvas(shell);
    if (!webgl) throw new Error('WebGL unavailable for cover open');
    const { gl, width, height } = webgl;
    const program = readerCreateWebGlProgram(gl);
    const buffers = {
      position: gl.createBuffer(),
      texCoord: gl.createBuffer(),
      aPosition: gl.getAttribLocation(program, 'a_position'),
      aTexCoord: gl.getAttribLocation(program, 'a_texCoord'),
      uTexture: gl.getUniformLocation(program, 'u_texture'),
      uAlpha: gl.getUniformLocation(program, 'u_alpha'),
      uShadow: gl.getUniformLocation(program, 'u_shadow')
    };
    const coverTexture = readerCreateWebGlTexture(gl, coverImg);
    const pageTwoTexture = readerCreateWebGlTexture(gl, pageTwoImg);
    const pageThreeTexture = readerCreateWebGlTexture(gl, pageThreeImg);
    textures = [coverTexture, pageTwoTexture, pageThreeTexture];
    const pageThreeMesh = readerBuildWebGlQuad(pageThreeGeometry || coverTargetGeometry, width, height, false);

    const drawCoverCurlFrame = p => {
      gl.viewport(0, 0, webgl.canvas.width, webgl.canvas.height);
      gl.clear(gl.COLOR_BUFFER_BIT);
      const shadow = 0.62 + Math.sin(Math.PI * p) * 1.10;

      const pageThreeAlpha = p > 0.075 ? Math.min(1, (p - 0.075) / 0.15) : 0;
      const frontAlpha = p < 0.63 ? 1 : Math.max(0, 1 - (p - 0.63) / 0.14);
      const backAlpha = p > 0.56 ? Math.min(1, (p - 0.56) / 0.22) : 0;

      if (pageThreeAlpha > 0.01) {
        revealPageThreeStack();
        readerDrawWebGlMesh(gl, program, buffers, pageThreeTexture, pageThreeMesh, pageThreeAlpha, 0.20);
      }
      if (frontAlpha > 0.01) {
        const frontMesh = readerBuildWebGlCurlMesh({ geometry: coverTargetGeometry, dir: 'next', progress: p, shellWidth: width, shellHeight: height, backside: false });
        readerDrawWebGlMesh(gl, program, buffers, coverTexture, frontMesh, frontAlpha, shadow);
      }
      if (backAlpha > 0.01) {
        const backSettle = p > 0.56 ? Math.min(1, (p - 0.56) / 0.44) : 0;
        const backGeometry = mixGeometry(coverTargetGeometry, pageTwoCurlTargetGeometry, backSettle);
        const backMesh = readerBuildWebGlCurlMesh({ geometry: backGeometry, dir: 'next', progress: p, shellWidth: width, shellHeight: height, backside: true });
        readerDrawWebGlMesh(gl, program, buffers, pageTwoTexture, backMesh, backAlpha, shadow);
      }
    };

    drawCoverCurlFrame(0);
    if (overlay) { overlay.remove(); overlay = null; }
    await waitForReaderPaint();

    const duration = 1250;
    const start = performance.now();
    await new Promise(resolve => {
      const draw = now => {
        const raw = Math.min(1, Math.max(0, (now - start) / duration));
        const eased = raw < 0.5
          ? 4 * raw * raw * raw
          : 1 - Math.pow(-2 * raw + 2, 3) / 2;
        drawCoverCurlFrame(eased);
        if (raw < 1) requestAnimationFrame(draw);
        else resolve();
      };
      requestAnimationFrame(draw);
    });

    // Stage 3: the true page 2/page 3 DOM spread is already in the final footprint.
    // Restore the hidden DOM pages under the canvas, then fade the canvas away so the
    // handoff no longer changes the page-3 corner or the right-side page stack.
    renderSpread(nextIndex, { preserveSize: true });
    if (leftImage?.style) leftImage.style.visibility = previousLeftVisibility;
    if (coverImage?.style) coverImage.style.visibility = previousCoverVisibility;
    applyReaderShadingSettings();
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
    await waitForReaderPaint();
    await waitForReaderPaint();

    book.classList.remove('reader-cover-stack-hidden', 'reader-cover-opening-stage');
    if (webgl?.canvas) {
      webgl.canvas.style.transition = 'opacity 220ms ease-out';
      webgl.canvas.style.opacity = '0';
      await wait(230);
    }
  } catch (err) {
    console.warn('Guidevault cover WebGL reveal unavailable; falling back to stable cover open.', err);
    renderSpread(nextIndex, { preserveSize: true });
    if (leftImage?.style) leftImage.style.visibility = previousLeftVisibility;
    if (coverImage?.style) coverImage.style.visibility = previousCoverVisibility;
    applyReaderShadingSettings();
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
  } finally {
    if (leftImage?.style) leftImage.style.visibility = previousLeftVisibility;
    if (coverImage?.style) coverImage.style.visibility = previousCoverVisibility;
    if (overlay) overlay.remove();
    if (webgl?.gl) {
      textures.forEach(texture => { try { webgl.gl.deleteTexture(texture); } catch {} });
    }
    if (webgl?.canvas) webgl.canvas.remove();
    shell.classList.remove(
      'reader-page-turn-active',
      'reader-page-turn-webgl-active',
      'reader-cover-open-slide-phase',
      'reader-cover-webgl-open-active',
      'reader-webgl-shadow-settle'
    );
    book.classList.remove('reader-cover-open-active', 'reader-cover-stack-hidden', 'reader-cover-opening-stage', 'reader-cover-page3-stack-visible');
    unlockExperimentalTurnFootprint(book, shell);
  }
}




async function performReaderCoverOpenAdaptiveTransition(nextIndex) {
  const shell = $('pageShell');
  const book = $('book');
  const coverImage = $('pageRightImage');
  const leftImage = $('pageLeftImage');
  if (!shell || !book || !coverImage?.src) {
    renderSpread(nextIndex, { preserveSize: true });
    return;
  }

  const toSpread = spreadForIndex(nextIndex);
  const coverUrl = coverImage.src;
  const adaptiveUrl = toSpread?.adaptiveUrl || toSpread?.rightUrl || readerPageUrlAt(nextIndex) || '';
  if (!adaptiveUrl) {
    renderSpread(nextIndex, { preserveSize: true });
    return;
  }

  let overlay = null;
  let webgl = null;
  let textures = [];
  const previousCoverVisibility = coverImage.style.visibility || '';
  const previousLeftVisibility = leftImage?.style?.visibility || '';

  try {
    const [coverImg, adaptiveImg] = await Promise.all([
      readerLoadImageForTexture(coverUrl),
      readerLoadImageForTexture(adaptiveUrl)
    ]);

    const containInRect = (rect, naturalW, naturalH, objectPosition = 'center') => {
      const ratio = naturalW > 0 && naturalH > 0 ? naturalW / naturalH : 0.735;
      let width = rect.width;
      let height = width / ratio;
      if (height > rect.height) {
        height = rect.height;
        width = height * ratio;
      }
      const extraX = Math.max(0, rect.width - width);
      const leftOffset = objectPosition === 'left' ? 0 : objectPosition === 'right' ? extraX : extraX / 2;
      return {
        left: rect.left + leftOffset,
        top: rect.top + Math.max(0, (rect.height - height) / 2),
        width: Math.max(8, width),
        height: Math.max(8, height)
      };
    };

    const currentRightPage = $('pageRight');
    const currentPageRect = currentRightPage ? {
      left: (shell.offsetLeft || 0) + (currentRightPage.offsetLeft || 0),
      top: (shell.offsetTop || 0) + (currentRightPage.offsetTop || 0),
      width: currentRightPage.clientWidth || currentRightPage.offsetWidth || Math.max(1, (book.offsetWidth || 1) / 2),
      height: currentRightPage.clientHeight || currentRightPage.offsetHeight || Math.max(1, book.offsetHeight || 1)
    } : {
      left: Math.max(0, (book.offsetWidth || 1) * .25),
      top: 0,
      width: Math.max(1, (book.offsetWidth || 1) * .5),
      height: Math.max(1, book.offsetHeight || 1)
    };
    const fromGeometry = containInRect(currentPageRect, coverImg.naturalWidth || 0, coverImg.naturalHeight || 0, 'center');

    overlay = document.createElement('div');
    overlay.className = 'reader-cover-open-overlay reader-cover-open-webgl-slide';
    applyReaderTurnGeometry(overlay, fromGeometry);
    overlay.style.transform = 'translate3d(0,0,0) scale(1,1)';
    overlay.style.transformOrigin = 'left top';
    const overlayImg = document.createElement('img');
    overlayImg.alt = '';
    overlayImg.src = coverUrl;
    overlay.appendChild(overlayImg);
    book.appendChild(overlay);

    book.classList.add('reader-cover-open-active', 'reader-cover-stack-hidden', 'reader-cover-opening-stage');
    shell.classList.add('reader-cover-open-slide-phase');
    if (coverImage?.style) coverImage.style.visibility = 'hidden';
    if (leftImage?.style) leftImage.style.visibility = 'hidden';

    renderSpread(nextIndex, { preserveSize: true });
    const targetRightImage = $('pageRightImage');
    const targetLeftImage = $('pageLeftImage');
    if (targetRightImage?.style) targetRightImage.style.visibility = 'hidden';
    if (targetLeftImage?.style) targetLeftImage.style.visibility = 'hidden';
    lockExperimentalTurnFootprint(book, shell);
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
    await waitForReaderPaint();
    await waitForReaderPaint();

    const pageRight = $('pageRight');
    const adaptiveGeometry = readerAdaptiveSpreadGeometry(shell, adaptiveImg)
      || readerCenteredContainGeometry(pageRight, adaptiveImg)
      || { left: 0, top: 0, width: shell.clientWidth || book.clientWidth || 1, height: shell.clientHeight || book.clientHeight || 1 };

    const coverSlotRect = pageRight ? {
      left: (pageRight.offsetLeft || 0) + Math.max(0, (pageRight.clientWidth || 0) / 2),
      top: (pageRight.offsetTop || 0),
      width: Math.max(8, (pageRight.clientWidth || shell.clientWidth || book.clientWidth || 1) / 2),
      height: Math.max(8, pageRight.clientHeight || pageRight.offsetHeight || shell.clientHeight || book.clientHeight || 1)
    } : {
      left: Math.max(0, (shell.clientWidth || book.clientWidth || 1) / 2),
      top: 0,
      width: Math.max(8, (shell.clientWidth || book.clientWidth || 1) / 2),
      height: Math.max(8, shell.clientHeight || book.clientHeight || 1)
    };
    const coverTargetGeometry = containInRect(coverSlotRect, coverImg.naturalWidth || 0, coverImg.naturalHeight || 0, 'right');

    const moveX = coverTargetGeometry.left - fromGeometry.left;
    const moveY = coverTargetGeometry.top - fromGeometry.top;
    const scaleX = coverTargetGeometry.width / Math.max(1, fromGeometry.width);
    const scaleY = coverTargetGeometry.height / Math.max(1, fromGeometry.height);
    const transformAt = (k) => {
      const sx = 1 + (scaleX - 1) * k;
      const sy = 1 + (scaleY - 1) * k;
      return `translate3d(${(moveX * k).toFixed(2)}px,${(moveY * k).toFixed(2)}px,0) scale(${sx.toFixed(5)},${sy.toFixed(5)})`;
    };

    await overlay.animate([
      { transform: transformAt(0), opacity: 1, filter: 'brightness(1)', offset: 0 },
      { transform: transformAt(.26), opacity: 1, filter: 'brightness(1)', offset: .30 },
      { transform: transformAt(.72), opacity: 1, filter: 'brightness(1)', offset: .74 },
      { transform: transformAt(1), opacity: 1, filter: 'brightness(1)', offset: 1 }
    ], { duration: 1060, easing: 'cubic-bezier(.20,.74,.16,1)', fill: 'forwards' }).finished.catch(() => {});

    shell.classList.remove('reader-cover-open-slide-phase');

    webgl = readerCreateWebGlPageCurlCanvas(shell);
    if (!webgl) throw new Error('WebGL unavailable for adaptive cover open');
    const { gl, width, height } = webgl;
    const program = readerCreateWebGlProgram(gl);
    const buffers = {
      position: gl.createBuffer(),
      texCoord: gl.createBuffer(),
      aPosition: gl.getAttribLocation(program, 'a_position'),
      aTexCoord: gl.getAttribLocation(program, 'a_texCoord'),
      uTexture: gl.getUniformLocation(program, 'u_texture'),
      uAlpha: gl.getUniformLocation(program, 'u_alpha'),
      uShadow: gl.getUniformLocation(program, 'u_shadow')
    };
    const coverTexture = readerCreateWebGlTexture(gl, coverImg);
    const adaptiveTexture = readerCreateWebGlTexture(gl, adaptiveImg);
    textures = [coverTexture, adaptiveTexture];
    const adaptiveMesh = readerBuildWebGlQuad(adaptiveGeometry, width, height, false);

    const drawCoverAdaptiveFrame = p => {
      gl.viewport(0, 0, webgl.canvas.width, webgl.canvas.height);
      gl.clear(gl.COLOR_BUFFER_BIT);
      const shadow = 0.62 + Math.sin(Math.PI * p) * 1.10;

      // Do not expose the whole destination spread immediately. The previous pass
      // showed the destination left half and the static front cover side-by-side at
      // the start of the first adaptive turn, which made it look unchanged from the
      // earlier broken state. Let the adaptive spread fade/reveal underneath only as
      // the cover begins to actually curl away.
      const revealAlpha = p > 0.18 ? Math.min(1, (p - 0.18) / 0.34) : 0;
      if (revealAlpha > 0.01) {
        readerDrawWebGlMesh(gl, program, buffers, adaptiveTexture, adaptiveMesh, revealAlpha, 0.18);
      }

      const curlProgress = Math.min(1, Math.max(0, p));
      const frontAlpha = p < 0.58 ? 1 : Math.max(0, 1 - (p - 0.58) / 0.20);
      const backAlpha = p > 0.36 ? Math.min(1, (p - 0.36) / 0.30) : 0;
      if (frontAlpha > 0.01) {
        const frontMesh = readerBuildWebGlCurlMesh({ geometry: coverTargetGeometry, dir: 'next', progress: curlProgress, shellWidth: width, shellHeight: height, backside: false });
        readerDrawWebGlMesh(gl, program, buffers, coverTexture, frontMesh, frontAlpha, shadow);
      }
      if (backAlpha > 0.01) {
        const backMesh = readerBuildWebGlCurlMesh({ geometry: coverTargetGeometry, dir: 'next', progress: curlProgress, shellWidth: width, shellHeight: height, backside: true });
        readerDrawWebGlMesh(gl, program, buffers, adaptiveTexture, backMesh, backAlpha, shadow);
      }
    };

    drawCoverAdaptiveFrame(0);
    if (overlay) { overlay.remove(); overlay = null; }
    await waitForReaderPaint();

    const duration = 1320;
    const start = performance.now();
    await new Promise(resolve => {
      const draw = now => {
        const raw = Math.min(1, Math.max(0, (now - start) / duration));
        const eased = raw < 0.5
          ? 4 * raw * raw * raw
          : 1 - Math.pow(-2 * raw + 2, 3) / 2;
        drawCoverAdaptiveFrame(eased);
        if (raw < 1) requestAnimationFrame(draw);
        else resolve();
      };
      requestAnimationFrame(draw);
    });

    renderSpread(nextIndex, { preserveSize: true });
    const finalRightImage = $('pageRightImage');
    const finalLeftImage = $('pageLeftImage');
    if (finalRightImage?.style) finalRightImage.style.visibility = '';
    if (finalLeftImage?.style) finalLeftImage.style.visibility = '';
    applyReaderShadingSettings();
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
    await waitForReaderPaint();
    await waitForReaderPaint();

    book.classList.remove('reader-cover-stack-hidden', 'reader-cover-opening-stage');
    if (webgl?.canvas) {
      webgl.canvas.style.transition = 'opacity 220ms ease-out';
      webgl.canvas.style.opacity = '0';
      await wait(230);
    }
  } catch (err) {
    console.warn('Guidevault adaptive cover reveal unavailable; falling back to stable adaptive open.', err);
    renderSpread(nextIndex, { preserveSize: true });
    applyReaderShadingSettings();
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
  } finally {
    if (coverImage?.style) coverImage.style.visibility = previousCoverVisibility;
    if (leftImage?.style) leftImage.style.visibility = previousLeftVisibility;
    const rightImage = $('pageRightImage');
    const freshLeftImage = $('pageLeftImage');
    if (rightImage?.style) rightImage.style.visibility = '';
    if (freshLeftImage?.style) freshLeftImage.style.visibility = previousLeftVisibility;
    if (overlay) overlay.remove();
    if (webgl?.gl) {
      textures.forEach(texture => { try { webgl.gl.deleteTexture(texture); } catch {} });
    }
    if (webgl?.canvas) webgl.canvas.remove();
    shell.classList.remove(
      'reader-page-turn-active',
      'reader-page-turn-webgl-active',
      'reader-cover-open-slide-phase',
      'reader-cover-webgl-open-active',
      'reader-webgl-shadow-settle'
    );
    book.classList.remove('reader-cover-open-active', 'reader-cover-stack-hidden', 'reader-cover-opening-stage', 'reader-cover-page3-stack-visible');
    unlockExperimentalTurnFootprint(book, shell);
  }
}

async function performReaderCoverCloseTransition(nextIndex) {
  const shell = $('pageShell');
  const book = $('book');
  const leftImage = $('pageLeftImage');
  const rightImage = $('pageRightImage');
  if (!shell || !book || !leftImage?.src || !rightImage?.src) {
    renderSpread(nextIndex, { preserveSize: true });
    return;
  }

  const fromSpread = spreadForIndex(state.reader.index);
  const coverUrl = readerPageUrlAt(0) || '';
  const pageTwoUrl = fromSpread?.leftUrl || readerPageUrlAt(1) || leftImage.src || '';
  const pageThreeUrl = fromSpread?.rightUrl || readerPageUrlAt(2) || rightImage.src || '';
  if (!coverUrl) {
    renderSpread(nextIndex, { preserveSize: true });
    return;
  }

  let overlay = null;
  let webgl = null;
  let textures = [];
  const previousLeftVisibility = leftImage.style.visibility || '';
  const previousRightVisibility = rightImage.style.visibility || '';

  try {
    // Reverse of the settled front-cover open sequence:
    // 1) draw the current page-2/page-3 spread in WebGL and curl it closed into a
    //    right-slot front cover,
    // 2) switch the real DOM back to the centered front cover underneath,
    // 3) slide that closed cover from the right-page slot back to the centered cover pose.
    const [coverImg, pageTwoImg, pageThreeImg] = await Promise.all([
      readerLoadImageForTexture(coverUrl),
      readerLoadImageForTexture(pageTwoUrl || coverUrl),
      readerLoadImageForTexture(pageThreeUrl || pageTwoUrl || coverUrl)
    ]);

    const containInRect = (rect, naturalW, naturalH, objectPosition = 'center') => {
      const ratio = naturalW > 0 && naturalH > 0 ? naturalW / naturalH : 0.735;
      let width = rect.width;
      let height = width / ratio;
      if (height > rect.height) {
        height = rect.height;
        width = height * ratio;
      }
      const extraX = Math.max(0, rect.width - width);
      const leftOffset = objectPosition === 'left' ? 0 : objectPosition === 'right' ? extraX : extraX / 2;
      return {
        left: rect.left + leftOffset,
        top: rect.top + Math.max(0, (rect.height - height) / 2),
        width: Math.max(8, width),
        height: Math.max(8, height)
      };
    };

    const geometryFromPageAndImage = (pageEl, imageInfo, side) => {
      if (!pageEl || !imageInfo) return null;
      const pageW = pageEl.clientWidth || pageEl.offsetWidth || 0;
      const pageH = pageEl.clientHeight || pageEl.offsetHeight || 0;
      if (!pageW || !pageH) return null;
      const naturalW = imageInfo.naturalWidth || imageInfo.width || 0;
      const naturalH = imageInfo.naturalHeight || imageInfo.height || 0;
      const ratio = naturalW > 0 && naturalH > 0 ? naturalW / naturalH : 0.735;
      let imageW = pageW;
      let imageH = imageW / ratio;
      if (imageH > pageH) {
        imageH = pageH;
        imageW = imageH * ratio;
      }
      const extraX = Math.max(0, pageW - imageW);
      const x = side === 'left' ? extraX : side === 'right' ? 0 : extraX / 2;
      return {
        left: (pageEl.offsetLeft || 0) + x,
        top: (pageEl.offsetTop || 0) + Math.max(0, (pageH - imageH) / 2),
        width: Math.max(8, imageW),
        height: Math.max(8, imageH)
      };
    };

    const mixGeometry = (a, b, t) => {
      if (!a || !b) return a || b;
      const p = Math.max(0, Math.min(1, Number(t) || 0));
      return {
        left: a.left + (b.left - a.left) * p,
        top: a.top + (b.top - a.top) * p,
        width: a.width + (b.width - a.width) * p,
        height: a.height + (b.height - a.height) * p
      };
    };

    const pageLeft = $('pageLeft');
    const pageRight = $('pageRight');
    const pageTwoGeometry = geometryFromPageAndImage(pageLeft, pageTwoImg, 'left');
    const pageThreeGeometry = geometryFromPageAndImage(pageRight, pageThreeImg, 'right');
    const coverRightGeometry = geometryFromPageAndImage(pageRight, coverImg, 'right') || pageThreeGeometry || readerPageGeometry(pageRight, rightImage, 'right', shell);
    const pageTwoCurlTargetGeometry = pageTwoGeometry
      ? { left: pageTwoGeometry.left + pageTwoGeometry.width, top: pageTwoGeometry.top, width: pageTwoGeometry.width, height: pageTwoGeometry.height }
      : coverRightGeometry;

    book.classList.add('reader-cover-open-active', 'reader-cover-stack-hidden', 'reader-cover-opening-stage', 'reader-cover-page3-stack-visible');
    shell.classList.add('reader-cover-webgl-open-active');
    lockExperimentalTurnFootprint(book, shell);
    updateReaderPageStackEffect(fromSpread);
    updateReaderPageEdgeShadingBounds();

    webgl = readerCreateWebGlPageCurlCanvas(shell);
    if (!webgl) throw new Error('WebGL unavailable for cover close');
    const { gl, width, height } = webgl;
    const program = readerCreateWebGlProgram(gl);
    const buffers = {
      position: gl.createBuffer(),
      texCoord: gl.createBuffer(),
      aPosition: gl.getAttribLocation(program, 'a_position'),
      aTexCoord: gl.getAttribLocation(program, 'a_texCoord'),
      uTexture: gl.getUniformLocation(program, 'u_texture'),
      uAlpha: gl.getUniformLocation(program, 'u_alpha'),
      uShadow: gl.getUniformLocation(program, 'u_shadow')
    };
    const coverTexture = readerCreateWebGlTexture(gl, coverImg);
    const pageTwoTexture = readerCreateWebGlTexture(gl, pageTwoImg);
    const pageThreeTexture = readerCreateWebGlTexture(gl, pageThreeImg);
    textures = [coverTexture, pageTwoTexture, pageThreeTexture];
    const pageThreeMesh = readerBuildWebGlQuad(pageThreeGeometry || coverRightGeometry, width, height, false);

    const drawCoverCloseFrame = p => {
      gl.viewport(0, 0, webgl.canvas.width, webgl.canvas.height);
      gl.clear(gl.COLOR_BUFFER_BIT);
      const shadow = 0.62 + Math.sin(Math.PI * p) * 1.10;

      const pageThreeAlpha = p > 0.075 ? Math.min(1, (p - 0.075) / 0.15) : 0;
      const frontAlpha = p < 0.63 ? 1 : Math.max(0, 1 - (p - 0.63) / 0.14);
      const backAlpha = p > 0.56 ? Math.min(1, (p - 0.56) / 0.22) : 0;

      if (pageThreeAlpha > 0.01) {
        book.classList.add('reader-cover-page3-stack-visible');
        readerDrawWebGlMesh(gl, program, buffers, pageThreeTexture, pageThreeMesh, pageThreeAlpha, 0.20);
      } else {
        book.classList.remove('reader-cover-page3-stack-visible');
      }
      if (frontAlpha > 0.01) {
        const frontMesh = readerBuildWebGlCurlMesh({ geometry: coverRightGeometry, dir: 'next', progress: p, shellWidth: width, shellHeight: height, backside: false });
        readerDrawWebGlMesh(gl, program, buffers, coverTexture, frontMesh, frontAlpha, shadow);
      }
      if (backAlpha > 0.01) {
        const backSettle = p > 0.56 ? Math.min(1, (p - 0.56) / 0.44) : 0;
        const backGeometry = mixGeometry(coverRightGeometry, pageTwoCurlTargetGeometry, backSettle);
        const backMesh = readerBuildWebGlCurlMesh({ geometry: backGeometry, dir: 'next', progress: p, shellWidth: width, shellHeight: height, backside: true });
        readerDrawWebGlMesh(gl, program, buffers, pageTwoTexture, backMesh, backAlpha, shadow);
      }
    };

    // Draw the closing book state first so there is no empty frame before the DOM spread is hidden.
    drawCoverCloseFrame(1);
    if (leftImage?.style) leftImage.style.visibility = 'hidden';
    if (rightImage?.style) rightImage.style.visibility = 'hidden';
    await waitForReaderPaint();

    const duration = 1250;
    const start = performance.now();
    await new Promise(resolve => {
      const draw = now => {
        const raw = Math.min(1, Math.max(0, (now - start) / duration));
        const eased = raw < 0.5
          ? 4 * raw * raw * raw
          : 1 - Math.pow(-2 * raw + 2, 3) / 2;
        drawCoverCloseFrame(1 - eased);
        if (raw < 1) requestAnimationFrame(draw);
        else resolve();
      };
      requestAnimationFrame(draw);
    });

    // Switch the real DOM back to the centered cover, but keep it hidden while the
    // reverse slide carries the closed cover from the right-page slot to the cover pose.
    renderSpread(nextIndex, { preserveSize: true });
    shell.classList.add('reader-cover-open-slide-phase');
    if (leftImage?.style) leftImage.style.visibility = 'hidden';
    if (rightImage?.style) rightImage.style.visibility = 'hidden';
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
    await waitForReaderPaint();
    await waitForReaderPaint();

    const centeredRightPage = $('pageRight');
    const centeredPageRect = centeredRightPage ? {
      left: (shell.offsetLeft || 0) + (centeredRightPage.offsetLeft || 0),
      top: (shell.offsetTop || 0) + (centeredRightPage.offsetTop || 0),
      width: centeredRightPage.clientWidth || centeredRightPage.offsetWidth || Math.max(1, (book.offsetWidth || 1) / 2),
      height: centeredRightPage.clientHeight || centeredRightPage.offsetHeight || Math.max(1, book.offsetHeight || 1)
    } : {
      left: Math.max(0, (book.offsetWidth || 1) * .25),
      top: 0,
      width: Math.max(1, (book.offsetWidth || 1) * .5),
      height: Math.max(1, book.offsetHeight || 1)
    };
    const centeredCoverGeometry = containInRect(centeredPageRect, coverImg.naturalWidth || 0, coverImg.naturalHeight || 0, 'center');

    overlay = document.createElement('div');
    overlay.className = 'reader-cover-open-overlay reader-cover-open-webgl-slide';
    applyReaderTurnGeometry(overlay, coverRightGeometry);
    overlay.style.transform = 'translate3d(0,0,0) scale(1,1)';
    overlay.style.transformOrigin = 'left top';
    const overlayImg = document.createElement('img');
    overlayImg.alt = '';
    overlayImg.src = coverUrl;
    overlay.appendChild(overlayImg);
    book.appendChild(overlay);

    const moveX = centeredCoverGeometry.left - coverRightGeometry.left;
    const moveY = centeredCoverGeometry.top - coverRightGeometry.top;
    const scaleX = centeredCoverGeometry.width / Math.max(1, coverRightGeometry.width);
    const scaleY = centeredCoverGeometry.height / Math.max(1, coverRightGeometry.height);
    const transformAt = k => {
      const sx = 1 + (scaleX - 1) * k;
      const sy = 1 + (scaleY - 1) * k;
      return `translate3d(${(moveX * k).toFixed(2)}px,${(moveY * k).toFixed(2)}px,0) scale(${sx.toFixed(5)},${sy.toFixed(5)})`;
    };

    if (webgl?.canvas) {
      webgl.canvas.style.transition = 'opacity 120ms linear';
      webgl.canvas.style.opacity = '0';
      await wait(130);
    }
    if (webgl?.canvas) {
      webgl.canvas.remove();
      webgl = null;
    }

    await overlay.animate([
      { transform: transformAt(0), opacity: 1, filter: 'brightness(1)', offset: 0 },
      { transform: transformAt(.28), opacity: 1, filter: 'brightness(1)', offset: .32 },
      { transform: transformAt(.76), opacity: 1, filter: 'brightness(1)', offset: .76 },
      { transform: transformAt(1), opacity: 1, filter: 'brightness(1)', offset: 1 }
    ], { duration: 1060, easing: 'cubic-bezier(.20,.74,.16,1)', fill: 'forwards' }).finished.catch(() => {});

    shell.classList.remove('reader-cover-open-slide-phase');
    if (leftImage?.style) leftImage.style.visibility = previousLeftVisibility;
    if (rightImage?.style) rightImage.style.visibility = previousRightVisibility;
    applyReaderShadingSettings();
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
    await waitForReaderPaint();
    await waitForReaderPaint();
  } catch (err) {
    console.warn('Guidevault cover WebGL close unavailable; falling back to stable cover close.', err);
    renderSpread(nextIndex, { preserveSize: true });
    if (leftImage?.style) leftImage.style.visibility = previousLeftVisibility;
    if (rightImage?.style) rightImage.style.visibility = previousRightVisibility;
    applyReaderShadingSettings();
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
  } finally {
    if (leftImage?.style) leftImage.style.visibility = previousLeftVisibility;
    if (rightImage?.style) rightImage.style.visibility = previousRightVisibility;
    if (overlay) overlay.remove();
    if (webgl?.gl) {
      textures.forEach(texture => { try { webgl.gl.deleteTexture(texture); } catch {} });
    }
    if (webgl?.canvas) webgl.canvas.remove();
    shell.classList.remove(
      'reader-page-turn-active',
      'reader-page-turn-webgl-active',
      'reader-cover-open-slide-phase',
      'reader-cover-webgl-open-active',
      'reader-webgl-shadow-settle'
    );
    book.classList.remove('reader-cover-open-active', 'reader-cover-stack-hidden', 'reader-cover-opening-stage', 'reader-cover-page3-stack-visible');
    unlockExperimentalTurnFootprint(book, shell);
  }
}


function readerAdaptiveSpreadGeometry(shell, imageInfo = null) {
  const page = $('pageRight');
  const image = $('pageRightImage');
  if (!page || !shell) return null;
  const pageW = page.clientWidth || page.offsetWidth || 0;
  const pageH = page.clientHeight || page.offsetHeight || 0;
  if (!pageW || !pageH) return null;
  const naturalW = Number(imageInfo?.naturalWidth || imageInfo?.width || image?.naturalWidth || 0);
  const naturalH = Number(imageInfo?.naturalHeight || imageInfo?.height || image?.naturalHeight || 0);
  if (!naturalW || !naturalH) return readerPageGeometry(page, image, 'right', shell);
  const ratio = naturalW / naturalH;
  let imageW = pageW;
  let imageH = imageW / ratio;
  if (imageH > pageH) {
    imageH = pageH;
    imageW = imageH * ratio;
  }
  return {
    left: (page.offsetLeft || 0) + Math.max(0, (pageW - imageW) / 2),
    top: (page.offsetTop || 0) + Math.max(0, (pageH - imageH) / 2),
    width: Math.max(8, imageW),
    height: Math.max(8, imageH)
  };
}

function readerGeometryHalf(geometry, side) {
  if (!geometry) return null;
  const width = Math.max(8, geometry.width / 2);
  return side === 'left'
    ? { left: geometry.left, top: geometry.top, width, height: geometry.height }
    : { left: geometry.left + width, top: geometry.top, width, height: geometry.height };
}

function readerBuildWebGlQuadRange(geometry, shellWidth, shellHeight, texRange = [0, 1]) {
  const minU = Math.max(0, Math.min(1, Number(texRange?.[0] ?? 0)));
  const maxU = Math.max(0, Math.min(1, Number(texRange?.[1] ?? 1)));
  const x0 = geometry.left;
  const x1 = geometry.left + geometry.width;
  const y0 = geometry.top;
  const y1 = geometry.top + geometry.height;
  const p00 = readerClipPoint(x0, y0, shellWidth, shellHeight);
  const p10 = readerClipPoint(x1, y0, shellWidth, shellHeight);
  const p01 = readerClipPoint(x0, y1, shellWidth, shellHeight);
  const p11 = readerClipPoint(x1, y1, shellWidth, shellHeight);
  return {
    positions: new Float32Array([
      p00[0], p00[1], p10[0], p10[1], p01[0], p01[1],
      p01[0], p01[1], p10[0], p10[1], p11[0], p11[1]
    ]),
    texCoords: new Float32Array([
      minU,0, maxU,0, minU,1,
      minU,1, maxU,0, maxU,1
    ])
  };
}

async function performReaderAdaptiveSpreadTurn(nextIndex, dir) {
  const shell = $('pageShell');
  const book = $('book');
  const sourceImage = $('pageRightImage');
  if (!shell || !book || !sourceImage?.src || !readerCanUseWebGlPageCurl()) {
    renderSpread(nextIndex, { preserveSize: true });
    return;
  }

  const fromSpread = spreadForIndex(state.reader.index);
  const toSpread = spreadForIndex(nextIndex);
  const sourceUrl = fromSpread?.adaptiveUrl || sourceImage.src;
  const targetUrl = toSpread?.adaptiveUrl || toSpread?.rightUrl || readerPageUrlAt(nextIndex) || sourceUrl;
  let webgl = null;
  let textures = [];
  let sourceOriginalVisibility = sourceImage.style.visibility || '';

  try {
    const [frontImg, targetImg] = await Promise.all([
      readerLoadImageForTexture(sourceUrl),
      readerLoadImageForTexture(targetUrl)
    ]);

    lockExperimentalTurnFootprint(book, shell);
    shell.classList.add('reader-page-turn-active', 'reader-page-turn-webgl-active', 'reader-adaptive-spread-turn-active');
    book.classList.add('reader-adaptive-spread-turning');

    const sourceGeometry = readerAdaptiveSpreadGeometry(shell, frontImg);
    if (!sourceGeometry) throw new Error('Missing adaptive spread geometry');

    // Adaptive scans already contain two pages in one image. A page turn should lift
    // only the active half while the other half of the current spread stays planted.
    const leftGeometry = readerGeometryHalf(sourceGeometry, 'left');
    const rightGeometry = readerGeometryHalf(sourceGeometry, 'right');
    const turningGeometry = dir === 'prev' ? leftGeometry : rightGeometry;
    const staticGeometry = dir === 'prev' ? rightGeometry : leftGeometry;
    const frontTexRange = dir === 'prev' ? [0, 0.5] : [0.5, 1];
    const staticTexRange = dir === 'prev' ? [0.5, 1] : [0, 0.5];
    const backTexRange = dir === 'prev' ? [0.5, 1] : [0, 0.5];

    webgl = readerCreateWebGlPageCurlCanvas(shell);
    if (!webgl) throw new Error('WebGL unavailable');
    const { gl, width, height } = webgl;
    const program = readerCreateWebGlProgram(gl);
    const buffers = {
      position: gl.createBuffer(),
      texCoord: gl.createBuffer(),
      aPosition: gl.getAttribLocation(program, 'a_position'),
      aTexCoord: gl.getAttribLocation(program, 'a_texCoord'),
      uTexture: gl.getUniformLocation(program, 'u_texture'),
      uAlpha: gl.getUniformLocation(program, 'u_alpha'),
      uShadow: gl.getUniformLocation(program, 'u_shadow')
    };
    const frontTexture = readerCreateWebGlTexture(gl, frontImg);
    const targetTexture = readerCreateWebGlTexture(gl, targetImg);
    textures = [frontTexture, targetTexture];
    const targetMesh = readerBuildWebGlQuad(sourceGeometry, width, height, false);
    const staticSourceMesh = readerBuildWebGlQuadRange(staticGeometry, width, height, staticTexRange);

    const drawAdaptiveFrame = p => {
      gl.viewport(0, 0, webgl.canvas.width, webgl.canvas.height);
      gl.clear(gl.COLOR_BUFFER_BIT);
      const shadow = 0.62 + Math.sin(Math.PI * p) * 1.08;

      // Destination spread stays underneath, but the non-turning half of the current
      // scan is redrawn above it so the whole scan does not appear to change/turn.
      readerDrawWebGlMesh(gl, program, buffers, targetTexture, targetMesh, 1, 0.18);
      const staticHoldAlpha = p < 0.88 ? 1 : Math.max(0, 1 - (p - 0.88) / 0.10);
      if (staticHoldAlpha > 0.01) {
        readerDrawWebGlMesh(gl, program, buffers, frontTexture, staticSourceMesh, staticHoldAlpha, 0.12);
      }

      const frontAlpha = p < 0.90 ? 1 : Math.max(0, 1 - (p - 0.90) / 0.09);
      const backAlpha = p > 0.42 ? Math.min(1, (p - 0.42) / 0.26) : 0;
      if (frontAlpha > 0.01) {
        const frontMesh = readerBuildWebGlCurlMesh({
          geometry: turningGeometry,
          dir,
          progress: p,
          shellWidth: width,
          shellHeight: height,
          backside: false,
          texRange: frontTexRange
        });
        readerDrawWebGlMesh(gl, program, buffers, frontTexture, frontMesh, frontAlpha, shadow);
      }
      if (backAlpha > 0.01) {
        const backMesh = readerBuildWebGlCurlMesh({
          geometry: turningGeometry,
          dir,
          progress: p,
          shellWidth: width,
          shellHeight: height,
          backside: true,
          texRange: backTexRange
        });
        readerDrawWebGlMesh(gl, program, buffers, targetTexture, backMesh, backAlpha, shadow);
      }
    };

    drawAdaptiveFrame(0);
    await waitForReaderPaint();
    drawAdaptiveFrame(0.012);
    await waitForReaderPaint();
    if (sourceImage?.style) sourceImage.style.visibility = 'hidden';

    const duration = 1180;
    const start = performance.now();
    await new Promise(resolve => {
      const draw = now => {
        const raw = Math.min(1, Math.max(0, (now - start) / duration));
        const eased = raw < 0.5
          ? 4 * raw * raw * raw
          : 1 - Math.pow(-2 * raw + 2, 3) / 2;
        drawAdaptiveFrame(eased);
        if (raw < 1) requestAnimationFrame(draw);
        else resolve();
      };
      requestAnimationFrame(draw);
    });
  } catch (err) {
    console.warn('Guidevault adaptive spread half-page curl unavailable; falling back to stable adaptive swap.', err);
  } finally {
    renderSpread(nextIndex, { preserveSize: true });
    if (sourceImage?.style) sourceImage.style.visibility = sourceOriginalVisibility;
    applyReaderShadingSettings();
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
    shell.classList.add('reader-webgl-shadow-settle');
    await waitForReaderPaint();
    await waitForReaderPaint();
    if (webgl?.canvas) {
      webgl.canvas.style.transition = 'opacity 140ms ease-out';
      webgl.canvas.style.opacity = '0';
      await wait(150);
    }
    if (webgl?.gl) {
      textures.forEach(texture => { try { webgl.gl.deleteTexture(texture); } catch {} });
    }
    if (webgl?.canvas) webgl.canvas.remove();
    shell.classList.remove('reader-page-turn-active', 'reader-page-turn-webgl-active', 'reader-adaptive-spread-turn-active', 'reader-webgl-shadow-settle');
    book.classList.remove('reader-adaptive-spread-turning');
    unlockExperimentalTurnFootprint(book, shell);
  }
}


async function performReaderCoverCloseAdaptiveTransition(nextIndex) {
  const shell = $('pageShell');
  const book = $('book');
  const sourceImage = $('pageRightImage');
  if (!shell || !book || !sourceImage?.src) {
    renderSpread(nextIndex, { preserveSize: true });
    return;
  }

  const coverUrl = readerPageUrlAt(0) || '';
  if (!coverUrl) {
    renderSpread(nextIndex, { preserveSize: true });
    return;
  }

  let overlay = null;
  const previousSourceVisibility = sourceImage.style.visibility || '';

  try {
    const coverImg = await readerLoadImageForTexture(coverUrl);
    const sourceGeometry = readerAdaptiveSpreadGeometry(shell) || readerPageGeometry($('pageRight'), sourceImage, 'right', shell);
    const startGeometry = sourceGeometry ? readerGeometryHalf(sourceGeometry, 'right') : null;
    if (!startGeometry) throw new Error('Missing adaptive cover close geometry');

    overlay = document.createElement('div');
    overlay.className = 'reader-cover-open-overlay reader-cover-open-webgl-slide reader-cover-close-adaptive-overlay';
    applyReaderTurnGeometry(overlay, startGeometry);
    overlay.style.transform = 'translate3d(0,0,0) scale(1,1)';
    overlay.style.transformOrigin = 'left top';
    overlay.style.opacity = '0';
    const overlayImg = document.createElement('img');
    overlayImg.alt = '';
    overlayImg.src = coverUrl;
    overlay.appendChild(overlayImg);
    book.appendChild(overlay);

    book.classList.add('reader-cover-open-active', 'reader-cover-stack-hidden', 'reader-cover-opening-stage');
    await overlay.animate([
      { opacity: 0, filter: 'brightness(.96)' },
      { opacity: 1, filter: 'brightness(1)' }
    ], { duration: 220, easing: 'ease-out', fill: 'forwards' }).finished.catch(() => {});

    if (sourceImage?.style) sourceImage.style.visibility = 'hidden';
    renderSpread(nextIndex, { preserveSize: true });
    const coverImage = $('pageRightImage');
    if (coverImage?.style) coverImage.style.visibility = 'hidden';
    await waitForReaderPaint();
    await waitForReaderPaint();

    const currentRightPage = $('pageRight');
    const coverPageRect = currentRightPage ? {
      left: (shell.offsetLeft || 0) + (currentRightPage.offsetLeft || 0),
      top: (shell.offsetTop || 0) + (currentRightPage.offsetTop || 0),
      width: currentRightPage.clientWidth || currentRightPage.offsetWidth || Math.max(1, (book.offsetWidth || 1) / 2),
      height: currentRightPage.clientHeight || currentRightPage.offsetHeight || Math.max(1, book.offsetHeight || 1)
    } : {
      left: Math.max(0, (book.offsetWidth || 1) * .25),
      top: 0,
      width: Math.max(1, (book.offsetWidth || 1) * .5),
      height: Math.max(1, book.offsetHeight || 1)
    };
    const ratio = coverImg.naturalWidth > 0 && coverImg.naturalHeight > 0 ? coverImg.naturalWidth / coverImg.naturalHeight : 0.735;
    let coverWidth = coverPageRect.width;
    let coverHeight = coverWidth / ratio;
    if (coverHeight > coverPageRect.height) {
      coverHeight = coverPageRect.height;
      coverWidth = coverHeight * ratio;
    }
    const targetGeometry = {
      left: coverPageRect.left + Math.max(0, (coverPageRect.width - coverWidth) / 2),
      top: coverPageRect.top + Math.max(0, (coverPageRect.height - coverHeight) / 2),
      width: Math.max(8, coverWidth),
      height: Math.max(8, coverHeight)
    };

    const moveX = targetGeometry.left - startGeometry.left;
    const moveY = targetGeometry.top - startGeometry.top;
    const scaleX = targetGeometry.width / Math.max(1, startGeometry.width);
    const scaleY = targetGeometry.height / Math.max(1, startGeometry.height);
    const transformAt = (k) => {
      const sx = 1 + (scaleX - 1) * k;
      const sy = 1 + (scaleY - 1) * k;
      return `translate3d(${(moveX * k).toFixed(2)}px,${(moveY * k).toFixed(2)}px,0) scale(${sx.toFixed(5)},${sy.toFixed(5)})`;
    };

    await overlay.animate([
      { transform: transformAt(0), opacity: 1, offset: 0 },
      { transform: transformAt(.45), opacity: 1, offset: .46 },
      { transform: transformAt(1), opacity: 1, offset: 1 }
    ], { duration: 820, easing: 'cubic-bezier(.20,.74,.16,1)', fill: 'forwards' }).finished.catch(() => {});

    if (coverImage?.style) coverImage.style.visibility = '';
    applyReaderShadingSettings();
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
    await waitForReaderPaint();
  } catch (err) {
    console.warn('Guidevault adaptive cover close unavailable; falling back to stable close.', err);
    renderSpread(nextIndex, { preserveSize: true });
    applyReaderShadingSettings();
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
  } finally {
    if (sourceImage?.style) sourceImage.style.visibility = previousSourceVisibility;
    const coverImage = $('pageRightImage');
    if (coverImage?.style) coverImage.style.visibility = '';
    if (overlay) overlay.remove();
    book.classList.remove('reader-cover-open-active', 'reader-cover-stack-hidden', 'reader-cover-opening-stage', 'reader-cover-page3-stack-visible');
  }
}

async function performReaderPageTurnExperimental(nextIndex, dir) {
  const shell = $('pageShell');
  const book = $('book');
  if (!shell || !book || !readerCanUseWebGlPageCurl()) { renderSpread(nextIndex, { preserveSize: true }); return; }

  const fromSpread = spreadForIndex(state.reader.index);
  const toSpread = spreadForIndex(nextIndex);
  const adaptiveMode = isReaderAdaptiveSpreadMode();
  const isAdaptiveSpreadTurn = adaptiveMode && !!fromSpread?.isAdaptiveSpread && !!toSpread?.isAdaptiveSpread;
  const isAdaptiveCoverOpenTurn = adaptiveMode && !!fromSpread?.isCover && !!toSpread?.isAdaptiveSpread && dir === 'next';
  const isAdaptiveCoverCloseTurn = adaptiveMode && !!toSpread?.isCover && !!fromSpread?.isAdaptiveSpread && dir === 'prev';
  const isCoverOpenTurn = !!fromSpread?.isCover && dir === 'next' && state.reader.displayMode !== 1 && !adaptiveMode;
  const isCoverCloseTurn = !!toSpread?.isCover && dir === 'prev' && !fromSpread?.isSingle && !fromSpread?.isAdaptiveSpread && state.reader.displayMode !== 1;
  const isTwoPageSpreadTurn = !adaptiveMode && !fromSpread?.isSingle && !toSpread?.isSingle && !fromSpread?.isCover && !toSpread?.isCover && state.reader.displayMode !== 1;
  if (isAdaptiveCoverOpenTurn) {
    await performReaderCoverOpenAdaptiveTransition(nextIndex);
    return;
  }
  if (isAdaptiveCoverCloseTurn) {
    await performReaderCoverCloseAdaptiveTransition(nextIndex);
    return;
  }
  if (isCoverOpenTurn) {
    await performReaderCoverOpenTransition(nextIndex);
    return;
  }
  if (isCoverCloseTurn) {
    await performReaderCoverCloseTransition(nextIndex);
    return;
  }
  if (isAdaptiveSpreadTurn) {
    await performReaderAdaptiveSpreadTurn(nextIndex, dir);
    return;
  }
  if (!isTwoPageSpreadTurn) {
    renderSpread(nextIndex, { preserveSize: true });
    return;
  }

  const source = readerPageTurnSource(dir);
  if (!source?.page || !source?.image?.src) { renderSpread(nextIndex, { preserveSize: true }); return; }

  const sourceGeometry = readerPageGeometry(source.page, source.image, source.side, shell);
  if (!sourceGeometry) { renderSpread(nextIndex, { preserveSize: true }); return; }

  const underlayUrl = readerPageTurnUnderlayUrl(nextIndex, dir, source);
  const backUrl = readerPageTurnBackUrl(nextIndex, dir, source) || underlayUrl || source.image.src;
  const sourceUrl = source.image.src;
  let sourceOriginalVisibility = '';
  let sourceHidden = false;
  const hideTarget = source?.image || source?.page || null;
  let webgl = null;
  let textures = [];

  try {
    // Preload images before touching the visible reader. This prevents the brief flash
    // that happened when the live page was hidden while textures were still loading.
    const [frontImg, backImg, underlayImg] = await Promise.all([
      readerLoadImageForTexture(sourceUrl),
      readerLoadImageForTexture(backUrl),
      underlayUrl ? readerLoadImageForTexture(underlayUrl).catch(() => null) : Promise.resolve(null)
    ]);

    lockExperimentalTurnFootprint(book, shell);
    shell.classList.add('reader-page-turn-active', 'reader-page-turn-webgl-active');

    webgl = readerCreateWebGlPageCurlCanvas(shell);
    if (!webgl) throw new Error('WebGL unavailable');
    const { gl, width, height } = webgl;
    const program = readerCreateWebGlProgram(gl);
    const buffers = {
      position: gl.createBuffer(),
      texCoord: gl.createBuffer(),
      aPosition: gl.getAttribLocation(program, 'a_position'),
      aTexCoord: gl.getAttribLocation(program, 'a_texCoord'),
      uTexture: gl.getUniformLocation(program, 'u_texture'),
      uAlpha: gl.getUniformLocation(program, 'u_alpha'),
      uShadow: gl.getUniformLocation(program, 'u_shadow')
    };

    const frontTexture = readerCreateWebGlTexture(gl, frontImg);
    const backTexture = readerCreateWebGlTexture(gl, backImg);
    const underlayTexture = underlayImg ? readerCreateWebGlTexture(gl, underlayImg) : null;
    textures = [frontTexture, backTexture, underlayTexture].filter(Boolean);

    const underlayMesh = underlayTexture ? readerBuildWebGlQuad(sourceGeometry, width, height, false) : null;
    const sourceHoldMesh = readerBuildWebGlQuad(sourceGeometry, width, height, false);
    const duration = 980;

    const drawCurlFrame = p => {
      gl.viewport(0, 0, webgl.canvas.width, webgl.canvas.height);
      gl.clear(gl.COLOR_BUFFER_BIT);

      // Keep the source page opaque for the first few frames while the DOM image is
      // being handed to WebGL. This covers tiny antialias/mesh seams that custom
      // reader backgrounds made look like a transparent flash at turn start.
      // Keep an opaque page under the curling mesh at the start of the turn.
      // With photographic backgrounds, even tiny transparent mesh seams are visible,
      // so the destination underlay is now fully opaque immediately and the source
      // hold stays solid for the initial lift before fading away.
      const sourceHoldAlpha = p < 0.22 ? 1 : (p < 0.38 ? Math.max(0, 1 - ((p - 0.22) / 0.16)) : 0);
      if (underlayTexture && underlayMesh) {
        readerDrawWebGlMesh(gl, program, buffers, underlayTexture, underlayMesh, 1, 0.16);
      }
      if (sourceHoldAlpha > 0.01) {
        readerDrawWebGlMesh(gl, program, buffers, frontTexture, sourceHoldMesh, sourceHoldAlpha, 0.08);
      }

      // Cross over from the current page face to the true backside page near the vertical curl.
      // Keep the handoff tight to avoid a visible flash/double exposure.
      const frontAlpha = p < 0.50 ? 1 : Math.max(0, 1 - (p - 0.50) / 0.075);
      const backAlpha = p > 0.47 ? Math.min(1, (p - 0.47) / 0.095) : 0;
      const shadow = 0.55 + Math.sin(Math.PI * p) * 1.15;

      if (frontAlpha > 0.01) {
        const frontMesh = readerBuildWebGlCurlMesh({ geometry: sourceGeometry, dir, progress: p, shellWidth: width, shellHeight: height, backside: false });
        readerDrawWebGlMesh(gl, program, buffers, frontTexture, frontMesh, frontAlpha, shadow);
      }
      if (backAlpha > 0.01) {
        const backMesh = readerBuildWebGlCurlMesh({ geometry: sourceGeometry, dir, progress: p, shellWidth: width, shellHeight: height, backside: true });
        readerDrawWebGlMesh(gl, program, buffers, backTexture, backMesh, backAlpha, shadow);
      }
    };

    // Draw the first WebGL frame before hiding the DOM page so there is no empty/flash frame.
    drawCurlFrame(0);
    sourceOriginalVisibility = hideTarget?.style?.visibility || '';
    if (hideTarget?.style) hideTarget.style.visibility = 'hidden';
    sourceHidden = true;
    await waitForReaderPaint();

    const start = performance.now();
    await new Promise(resolve => {
      const draw = now => {
        const raw = Math.min(1, Math.max(0, (now - start) / duration));
        const eased = raw < 0.5
          ? 4 * raw * raw * raw
          : 1 - Math.pow(-2 * raw + 2, 3) / 2;
        drawCurlFrame(eased);
        if (raw < 1) requestAnimationFrame(draw);
        else resolve();
      };
      requestAnimationFrame(draw);
    });
  } catch (err) {
    console.warn('Guidevault WebGL page curl unavailable; falling back to stable swap.', err);
  } finally {
    // Keep the WebGL canvas covering the reader while the final spread is painted,
    // then fade the canvas away after the normal binding/page-edge shadows have been
    // recalculated. This prevents the end-of-turn shadow pop.
    renderSpread(nextIndex, { preserveSize: true });
    if (sourceHidden && hideTarget?.style) hideTarget.style.visibility = sourceOriginalVisibility;
    applyReaderShadingSettings();
    updateReaderPageStackEffect(spreadForIndex(nextIndex));
    updateReaderPageEdgeShadingBounds();
    shell.classList.add('reader-webgl-shadow-settle');
    await waitForReaderPaint();
    await waitForReaderPaint();
    if (webgl?.canvas) {
      webgl.canvas.style.transition = 'opacity 140ms ease-out';
      webgl.canvas.style.opacity = '0';
      await wait(150);
    }
    if (webgl?.gl) {
      textures.forEach(texture => { try { webgl.gl.deleteTexture(texture); } catch {} });
    }
    if (webgl?.canvas) webgl.canvas.remove();
    shell.classList.remove('reader-page-turn-active', 'reader-page-turn-webgl-active', 'reader-page-turn-mesh-active', 'reader-page-turn-single-curl-active', 'reader-webgl-shadow-settle');
    unlockExperimentalTurnFootprint(book, shell);
  }
}
async function performReaderTransition(nextIndex, dir, mode) {
  const shell = $('pageShell');
  const selectedMode = mode || 'stable';
  if (!shell || selectedMode === 'stable') {
    renderSpread(nextIndex, { preserveSize: true });
    return;
  }
  if (selectedMode === 'page') {
    await performReaderPageTurnExperimental(nextIndex, dir);
    return;
  }

  clearReaderTransitionStyles(shell);
  shell.style.setProperty('will-change', 'opacity, transform', 'important');
  shell.style.setProperty('transition', 'none', 'important');
  shell.style.setProperty('opacity', '1', 'important');
  shell.style.setProperty('transform', 'translate3d(0,0,0)', 'important');
  await waitForReaderPaint();

  if (selectedMode === 'fade') {
    await animateReaderShell(shell, {
      transition: 'opacity 120ms ease-in',
      opacity: '.18',
      transform: 'translate3d(0,0,0)'
    }, 130);
    renderSpread(nextIndex, { preserveSize: true });
    shell.style.setProperty('transition', 'none', 'important');
    shell.style.setProperty('opacity', '.18', 'important');
    shell.style.setProperty('transform', 'translate3d(0,0,0)', 'important');
    await waitForReaderPaint();
    await animateReaderShell(shell, {
      transition: 'opacity 130ms ease-out',
      opacity: '1',
      transform: 'translate3d(0,0,0)'
    }, 140);
  } else if (selectedMode === 'dissolve') {
    await animateReaderShell(shell, {
      transition: 'opacity 95ms ease-in',
      opacity: '.42',
      transform: 'translate3d(0,0,0)'
    }, 105);
    renderSpread(nextIndex, { preserveSize: true });
    shell.style.setProperty('transition', 'none', 'important');
    shell.style.setProperty('opacity', '.42', 'important');
    shell.style.setProperty('transform', 'translate3d(0,0,0)', 'important');
    await waitForReaderPaint();
    await animateReaderShell(shell, {
      transition: 'opacity 115ms ease-out',
      opacity: '1',
      transform: 'translate3d(0,0,0)'
    }, 125);
  } else if (selectedMode === 'slide') {
    const outX = dir === 'prev' ? '22px' : '-22px';
    const inX = dir === 'prev' ? '-22px' : '22px';
    await animateReaderShell(shell, {
      transition: 'opacity 115ms ease-in, transform 115ms ease-in',
      opacity: '.12',
      transform: `translate3d(${outX},0,0)`
    }, 125);
    renderSpread(nextIndex, { preserveSize: true });
    shell.style.setProperty('transition', 'none', 'important');
    shell.style.setProperty('opacity', '.12', 'important');
    shell.style.setProperty('transform', `translate3d(${inX},0,0)`, 'important');
    await waitForReaderPaint();
    await animateReaderShell(shell, {
      transition: 'opacity 145ms ease-out, transform 145ms ease-out',
      opacity: '1',
      transform: 'translate3d(0,0,0)'
    }, 155);
  } else if (selectedMode === 'push') {
    const outX = dir === 'prev' ? '48px' : '-48px';
    const inX = dir === 'prev' ? '-48px' : '48px';
    await animateReaderShell(shell, {
      transition: 'opacity 105ms ease-in, transform 105ms ease-in',
      opacity: '.04',
      transform: `translate3d(${outX},0,0)`
    }, 115);
    renderSpread(nextIndex, { preserveSize: true });
    shell.style.setProperty('transition', 'none', 'important');
    shell.style.setProperty('opacity', '.04', 'important');
    shell.style.setProperty('transform', `translate3d(${inX},0,0)`, 'important');
    await waitForReaderPaint();
    await animateReaderShell(shell, {
      transition: 'opacity 155ms cubic-bezier(.18,.78,.2,1), transform 155ms cubic-bezier(.18,.78,.2,1)',
      opacity: '1',
      transform: 'translate3d(0,0,0)'
    }, 165);
  } else {
    renderSpread(nextIndex, { preserveSize: true });
  }

  clearReaderTransitionStyles(shell);
}

function preloadReaderImage(url) {
  if (!url) return Promise.resolve();
  return new Promise(resolve => {
    const img = new Image();
    const finish = () => {
      if (img.decode) {
        img.decode().then(resolve).catch(resolve);
      } else {
        resolve();
      }
    };
    img.onload = finish;
    img.onerror = () => resolve();
    img.src = url;
  });
}

function setSnapshotSpread(spread) {
  const snapshot = $('previousSpread');
  if (!snapshot || !spread) return;

  const prevLeft = $('prevLeft');
  const prevRight = $('prevRight');
  const prevLeftImage = $('prevLeftImage');
  const prevRightImage = $('prevRightImage');

  snapshot.className = 'previous-spread hidden';
  if (prevLeft) prevLeft.classList.toggle('hidden', !!spread.isCover);
  if (prevRight) prevRight.classList.remove('blank-page');

  if (spread.isCover) {
    if (prevRightImage) prevRightImage.src = spread.rightUrl || '';
    if (prevLeftImage) prevLeftImage.src = '';
    snapshot.classList.add('cover-snapshot');
    return;
  }

  snapshot.classList.remove('cover-snapshot');
  if (prevLeftImage) prevLeftImage.src = spread.leftUrl || '';
  if (prevRightImage) prevRightImage.src = spread.rightUrl || '';
  if (prevRight) prevRight.classList.toggle('blank-page', !!spread.isBlankRight);
}

function lockReaderFrame(book) {
  if (!book) return;
  // Do not re-measure or rewrite the reader width/height at the start of a page advance.
  // Rewriting the CSS variables here caused a tiny shrink/expand pulse and briefly exposed
  // the underlying reader frame. The book was already sized when the spread was rendered;
  // during a turn we only lock that existing footprint in place.
  const currentWidth = getComputedStyle(book).getPropertyValue('--guidevault-reader-width').trim();
  const currentHeight = getComputedStyle(book).getPropertyValue('--guidevault-reader-height').trim();
  if (!currentWidth || !currentHeight) {
    const rect = book.getBoundingClientRect();
    if (rect.width && rect.height) {
      book.style.setProperty('--guidevault-reader-width', `${Math.floor(rect.width)}px`);
      book.style.setProperty('--guidevault-reader-height', `${Math.floor(rect.height)}px`);
    }
  }
  book.classList.add('reader-sized', 'reader-frame-locked');
}

function unlockReaderFrame(book) {
  if (!book) return;
  // Keep the existing measured reader footprint after a page advance. Re-measuring here
  // caused a subtle shrink/bounce at the end of the transition even though the stage
  // size had not actually changed. Resize/fullscreen handlers still refresh the size.
  book.classList.remove('reader-frame-locked');
}

async function showPage(index, dir) {
  if (!state.reader.pages.length || state.reader.animating) return;

  const fromSpread = spreadForIndex(state.reader.index);
  let nextIndex;
  if (state.reader.displayMode === 1 || isReaderAdaptiveSpreadMode()) {
    nextIndex = state.reader.index + (dir === 'prev' ? -1 : 1);
  } else if (state.reader.index === 0 && dir === 'next') {
    nextIndex = 1;
  } else if (state.reader.index <= 1 && dir === 'prev') {
    nextIndex = 0;
  } else {
    nextIndex = state.reader.index + (dir === 'prev' ? -2 : 2);
  }
  nextIndex = Math.max(0, Math.min(nextIndex, state.reader.pages.length - 1));

  const toSpread = spreadForIndex(nextIndex);
  if (!fromSpread || !toSpread || toSpread.index === state.reader.index) return;

  state.reader.animating = true;

  const book = $('book');
  const turning = $('turningPage');
  const under = $('underPage');
  const snapshot = $('previousSpread');

  // v0.9.6: completely bypass the old snapshot/turning-page transition stack.
  // Those temporary layers had their own footprint/styling and caused the visible
  // frame flash plus the tiny shrink/bounce during page advance. For now the reader
  // performs an in-place, fixed-footprint spread swap; a new page-turn effect should
  // be rebuilt later on top of this stable base.
  book.classList.remove(
    'flipping-next', 'flipping-prev', 'page-turning',
    'snapshot-turn-next', 'snapshot-turn-prev',
    'soft-turn-next', 'soft-turn-prev'
  );
  if (turning) turning.className = 'turning-page hidden';
  if (under) under.className = 'under-page hidden';
  if (snapshot) snapshot.className = 'previous-spread hidden';

  if (!book.classList.contains('reader-sized')) applyReaderBookSize(fromSpread);
  lockReaderFrame(book);

  const preloadTargets = [toSpread.leftUrl, toSpread.rightUrl].filter(Boolean);
  await Promise.all(preloadTargets.map(preloadReaderImage));
  if (!state.reader.animating) return;

  await performReaderTransition(nextIndex, dir, state.reader.transitionMode || 'stable');

  // Keep hidden helper layers hidden after the swap. This prevents old CSS rules from
  // briefly drawing a panel around the book during the next paint.
  if (turning) turning.className = 'turning-page hidden';
  if (under) under.className = 'under-page hidden';
  if (snapshot) snapshot.className = 'previous-spread hidden';

  requestAnimationFrame(() => {
    state.reader.animating = false;
    updateReaderOverlay();
    requestAnimationFrame(() => unlockReaderFrame(book));
  });
}


function upsertLibraryTask({ id = null, title = 'Library activity', message = 'Working...', progress = 0, status = 'running', kind = 'library-scan' } = {}) {
  const taskId = id || `local-library-${Date.now()}-${Math.floor(Math.random() * 10000)}`;
  const task = {
    id: taskId,
    kind,
    title,
    status,
    message,
    progressPercent: Math.max(0, Math.min(100, Number(progress) || 0)),
    updatedAt: new Date().toISOString()
  };
  state.tasks = [task, ...(state.tasks || []).filter(t => String(t.id || t.Id) !== String(taskId))];
  // Do not auto-open the task panel for new activity; update the badge/list only.
  renderTaskMonitor();
  return taskId;
}

function updateLibraryTask(id, message, progress = null, status = null, title = null) {
  if (!id) return;
  let found = false;
  state.tasks = (state.tasks || []).map(task => {
    if (String(task.id || task.Id) !== String(id)) return task;
    found = true;
    return {
      ...task,
      title: title || task.title || task.Title || 'Library activity',
      status: status || task.status || task.Status || 'running',
      message: message || task.message || task.Message || '',
      progressPercent: progress === null ? (task.progressPercent ?? task.ProgressPercent ?? 0) : Math.max(0, Math.min(100, Number(progress) || 0)),
      updatedAt: new Date().toISOString()
    };
  });
  if (!found) {
    const inferredTitle = title || 'Library activity';
    const lowerTitle = String(inferredTitle).toLowerCase();
    const inferredKind = lowerTitle.includes('removal') ? 'library-removal' : lowerTitle.includes('cleanup') ? 'library-cleanup' : lowerTitle.includes('enrichment') ? 'library-enrichment' : 'library-scan';
    state.tasks = [{ id, kind: inferredKind, title: inferredTitle, status: status || 'running', message: message || 'Working...', progressPercent: Math.max(0, Math.min(100, Number(progress) || 0)), updatedAt: new Date().toISOString() }, ...(state.tasks || [])];
  }
  // Do not auto-open the task panel for task updates; keep showing the badge count.
  renderTaskMonitor();
}

function replaceLibraryTask(oldId, task) {
  const normalized = {
    id: task.id || task.Id || oldId,
    kind: task.kind || task.Kind || 'library-scan',
    title: task.title || task.Title || 'Library activity',
    status: task.status || task.Status || 'running',
    message: task.message || task.Message || 'Working...',
    progressPercent: Math.max(0, Math.min(100, Number(task.progressPercent ?? task.ProgressPercent ?? 0) || 0)),
    updatedAt: task.updatedAt || task.UpdatedAt || new Date().toISOString()
  };
  state.tasks = [normalized, ...(state.tasks || []).filter(t => {
    const id = String(t.id || t.Id);
    return id !== String(oldId) && id !== String(normalized.id);
  })];
  // Do not auto-open the task panel when a backend task replaces a local placeholder.
  renderTaskMonitor();
}

function taskStatusLabel(task) {
  const status = String(task?.status || '').toLowerCase();
  if (status === 'running') return 'Running';
  if (status === 'completed') return 'Completed';
  if (status === 'failed') return 'Failed';
  return task?.status || 'Queued';
}

function renderTaskMonitor() {
  const panel = $('taskMonitorPanel');
  const list = $('taskMonitorList');
  const badge = $('taskMonitorBadge');
  const title = document.querySelector('.task-panel-title');
  if (!list) return;
  const tasks = Array.isArray(state.tasks) ? state.tasks : [];
  const active = tasks.filter(t => String(t.status || '').toLowerCase() === 'running' || String(t.status || '').toLowerCase() === 'queued');
  if (title) {
    const clearable = tasks.some(t => !['running','queued'].includes(String(t.status || t.Status || '').toLowerCase()));
    title.innerHTML = `<span>Tasks</span><button id="taskClearBtn" class="task-clear-button" type="button" ${clearable ? '' : 'disabled'}>Clear</button>`;
  }
  if (badge) {
    badge.textContent = String(active.length);
    badge.classList.toggle('hidden', active.length === 0);
  }
  if (panel) panel.classList.toggle('hidden', !state.taskPanelVisible);
  if (!tasks.length) {
    list.innerHTML = '<div class="task-empty">No running tasks.</div>';
    return;
  }
  list.innerHTML = tasks.slice(0, 8).map(task => {
    const progress = Math.max(0, Math.min(100, Number(task.progressPercent ?? task.ProgressPercent ?? 0) || 0));
    const title = escapeHtml(task.title || task.Title || 'Task');
    const message = escapeHtml(task.message || task.Message || taskStatusLabel(task));
    const status = escapeHtml(taskStatusLabel(task));
    const updated = task.updatedAt || task.UpdatedAt || '';
    return `<div class="task-card">
      <strong>${title}</strong>
      <small>${status}${updated ? ` • ${escapeHtml(new Date(updated).toLocaleTimeString())}` : ''}</small>
      <small>${message}</small>
      <div class="task-progress" style="--task-progress:${progress}%"><span></span></div>
    </div>`;
  }).join('');
}

async function clearTaskMessages() {
  state.tasks = (state.tasks || []).filter(t => ['running','queued'].includes(String(t.status || t.Status || '').toLowerCase()));
  renderTaskMonitor();
  try { await fetch('/api/tasks/clear', { method: 'POST', cache: 'no-store' }); } catch {}
  await pollTasks(false);
}

function setTaskPanelVisible(visible) {
  state.taskPanelVisible = !!visible;
  renderTaskMonitor();
}

async function pollTasks(forceReloadLibrary = false) {
  try {
    const res = await fetch('/api/tasks', { cache: 'no-store' });
    if (!res.ok) return;
    const data = await res.json();
    const previousActive = (state.tasks || []).some(t => ['running','queued'].includes(String(t.status || '').toLowerCase()));
    state.tasks = Array.isArray(data?.tasks) ? data.tasks : [];
    const active = state.tasks.some(t => ['running','queued'].includes(String(t.status || '').toLowerCase()));
    const justCompletedLibraryActivity = (forceReloadLibrary || previousActive) && !active && state.tasks.some(t => {
      const kind = String(t.kind || t.Kind || '').toLowerCase();
      const status = String(t.status || t.Status || '').toLowerCase();
      return (kind === 'library-scan' || kind === 'library-removal' || kind === 'library-cleanup' || kind === 'library-enrichment') && status === 'completed';
    });
    renderTaskMonitor();
    if (justCompletedLibraryActivity) {
      const refreshTaskId = upsertLibraryTask({
        title: 'Library refresh',
        message: 'Refreshing library view...',
        progress: 92,
        kind: 'library-refresh'
      });
      installGlobalDetailDelegate();
      await loadLibrary();
      updateLibraryTask(refreshTaskId, 'Library view refreshed.', 100, 'completed', 'Library refresh');
    }
    if (active && !state.taskPollTimer) {
      state.taskPollTimer = window.setInterval(() => pollTasks(false), 1200);
    } else if (!active && state.taskPollTimer) {
      window.clearInterval(state.taskPollTimer);
      state.taskPollTimer = null;
    }
  } catch (err) {
    console.warn('Task polling failed', err);
  }
}


function countBy(items, getKey) {
  const map = new Map();
  (items || []).forEach(item => {
    const raw = getKey(item);
    const values = Array.isArray(raw) ? raw : [raw];
    values.map(v => String(v || '').trim()).filter(Boolean).forEach(value => map.set(value, (map.get(value) || 0) + 1));
  });
  return [...map.entries()].sort((a,b)=>b[1]-a[1] || a[0].localeCompare(b[0]));
}
function topItemsHtml(rows, unit = 'items', limit = 5) {
  const list = (rows || []).slice(0, limit);
  if (!list.length) return '<p class="sub">No data yet.</p>';
  return list.map(([label, value], index) => `<div class="statistics-rank-row"><span>${index + 1}</span><strong>${escapeHtml(label || '—')}</strong><em>${escapeHtml(String(value))} ${escapeHtml(unit)}</em></div>`).join('');
}
function classifyDeviceByScreen(item) {
  const text = [item?.platform, item?.browser, item?.screen, item?.userAgent].join(' ').toLowerCase();
  if (/ipad|tablet/.test(text)) return 'Tablet';
  if (/android|iphone|mobile/.test(text)) return 'Mobile';
  return 'Desktop';
}
function filteredStatsEvents() {
  const range = state.statistics.range || 'all';
  const now = Date.now();
  const cutoff = range === 'month' ? now - 31*86400000 : range === 'year' ? now - 366*86400000 : 0;
  return readReadingActivity().filter(e => dateValue(e.at) >= cutoff);
}
function filteredStatsItems() {
  const range = state.statistics.range || 'all';
  const now = Date.now();
  const cutoff = range === 'month' ? now - 31*86400000 : range === 'year' ? now - 366*86400000 : 0;
  if (!cutoff) return state.items || [];
  return (state.items || []).filter(i => {
    const t = dateValue(i.modified || i.Modified || i.addedAt || i.createdAt || '');
    return t && t >= cutoff;
  });
}

function statisticsSummaryIcon(key) {
  const icon = {
    'total-series': '<svg viewBox="0 0 24 24"><path d="M5 4.5h10.5a2.5 2.5 0 0 1 2.5 2.5v12.5H7.5A2.5 2.5 0 0 0 5 22V4.5Z"/><path d="M5 4.5A2.5 2.5 0 0 1 7.5 2H19v15.5"/></svg>',
    'total-items': '<svg viewBox="0 0 24 24"><path d="M4 5.5A2.5 2.5 0 0 1 6.5 3h11A2.5 2.5 0 0 1 20 5.5v13A2.5 2.5 0 0 1 17.5 21h-11A2.5 2.5 0 0 1 4 18.5v-13Z"/><path d="M8 8h8M8 12h8M8 16h5"/></svg>',
    'manuals': '<svg viewBox="0 0 24 24"><path d="M4.5 5.5h6A3.5 3.5 0 0 1 14 9v10.5a3.5 3.5 0 0 0-3.5-3.5h-6V5.5Z"/><path d="M14 9a3.5 3.5 0 0 1 3.5-3.5h2v10.5h-2A3.5 3.5 0 0 0 14 19.5"/></svg>',
    'strategy-guides': '<svg viewBox="0 0 24 24"><path d="M4 18V6l8-3 8 3v12l-8 3-8-3Z"/><path d="M12 3v18M4 6l8 3 8-3M8 11h2M14 11h2M8 15h2M14 15h2"/></svg>',
    'magazines': '<svg viewBox="0 0 24 24"><path d="M6 4h10a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3V5a1 1 0 0 1 1-1Z"/><path d="M8 8h7M8 11h7M8 14h4"/></svg>',
    'total-size': '<svg viewBox="0 0 24 24"><path d="M6 20h12a2 2 0 0 0 2-2V8l-5-5H6a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z"/><path d="M14 3v6h6M8 15h8"/></svg>',
    'file-types': '<svg viewBox="0 0 24 24"><path d="M4 7h7l2 2h7v8a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7Z"/><path d="M8 12h8M8 15h5"/></svg>',
    'read-events': '<svg viewBox="0 0 24 24"><path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6Z"/><circle cx="12" cy="12" r="3"/></svg>'
  };
  return icon[key] || icon['total-items'];
}

function renderStatistics() {
  if (!$('settingsStatisticsPanel')) return;
  const activeTab = state.statistics.activeTab || 'stats';
  const allItems = state.items || [];
  if (activeTab === 'management') {
    renderStatisticsManagement(allItems);
    return;
  }
  const events = filteredStatsEvents();
  const totalSize = allItems.reduce((sum, item) => sum + Number(item.sizeBytes || item.SizeBytes || 0), 0);
  const series = new Set(allItems.map(i => i.series || categoryOf(i) || i.kind).filter(Boolean));
  const summary = [
    ['total-series', 'Total Series', series.size],
    ['total-items', 'Total Items', allItems.length],
    ['manuals', 'Manuals', count('Manual')],
    ['strategy-guides', 'Strategy Guides', count('Strategy Guide')],
    ['magazines', 'Magazines', count('Magazine')],
    ['total-size', 'Total Size', fmtBytes(totalSize)],
    ['file-types', 'File Types', countBy(allItems, fileExtensionOf).length],
    ['read-events', 'Read Events', events.length]
  ];
  if ($('statisticsSummary')) $('statisticsSummary').innerHTML = summary.map(([iconKey,label,value]) => `<div class="statistics-summary-card"><i class="statistics-summary-icon" aria-hidden="true">${statisticsSummaryIcon(iconKey)}</i><span>${escapeHtml(label)}</span><b>${escapeHtml(String(value))}</b></div>`).join('');

  const viewedCounts = countBy(events, e => e.title || 'Unknown');
  const popularFallback = allItems.slice().sort((a,b)=>Number(b.pageCount||0)-Number(a.pageCount||0)).slice(0,5).map(i => [displayTitle(i), Number(i.pageCount||1)]);
  if ($('statisticsPopularBooks')) $('statisticsPopularBooks').innerHTML = topItemsHtml(viewedCounts.length ? viewedCounts : popularFallback, viewedCounts.length ? 'reads' : 'pages', 5);
  const recentIds = [...new Set(events.slice().reverse().map(e => e.id).filter(Boolean))];
  const itemLookup = new Map(allItems.map(item => [String(item.id || item.Id), item]));
  const recentItems = recentIds.map(id => itemLookup.get(String(id))).filter(Boolean).slice(0, 5);
  if ($('statisticsRecentViewed')) $('statisticsRecentViewed').innerHTML = recentItems.length ? recentItems.map(item => `<div class="statistics-book-row"><img loading="lazy" src="${coverUrl(item)}" alt="" /><span><strong>${escapeHtml(displayTitle(item))}</strong><em>${escapeHtml(item.kind || '')} • ${escapeHtml(item.year || '—')}</em></span></div>`).join('') : '<p class="sub">Open a few items to populate recent views.</p>';

  const topDefs = [
    ['Popular Libraries', countBy(allItems, item => item.kind), 'items'],
    ['Popular Platforms', countBy(allItems, item => libraryCategoryKeysForItem(item)), 'items'],
    ['Popular Publishers', countBy(allItems, item => item.publisher || 'Unknown'), 'items'],
    ['Popular Decades', countBy(allItems, item => decadeLabel(item.year)), 'items'],
    ['Popular Topics', countBy(allItems, item => [...(item.tags || []), ...(item.includedSections || []), ...(item.itemsCovered || [])]), 'uses'],
    ['Popular Formats', countBy(allItems, fileExtensionOf), 'files']
  ];
  if ($('statisticsTopLists')) $('statisticsTopLists').innerHTML = topDefs.map(([title, rows, unit]) => `<article class="settings-card statistics-top-card"><h3>${escapeHtml(title)}</h3>${topItemsHtml(rows, unit, 5)}</article>`).join('');

  const profile = state.auth.profile || readLoginProfile() || {};
  const userRows = countBy(events, e => e.user || profile.username || 'local user');
  if ($('statisticsActiveUsers')) $('statisticsActiveUsers').innerHTML = (userRows.length ? userRows : [[profile.username || profile.email || 'local user', 0]]).slice(0,4).map(([user, total]) => `<article class="settings-card statistics-user-card"><div class="statistics-avatar">${escapeHtml(String(user).charAt(0).toUpperCase() || 'U')}</div><div><strong>${escapeHtml(user)}</strong><span>${escapeHtml(String(total))} events</span></div></article>`).join('');

  renderStatisticsReadingActivity(events);
}
function decadeLabel(year) {
  const n = Number(String(year || '').match(/\d{4}/)?.[0] || 0);
  return n ? `${Math.floor(n/10)*10}s` : 'Unknown';
}
function renderStatisticsReadingActivity(events) {
  const groups = ['Manual','Strategy Guide','Magazine'];
  const colors = ['#6aa6ff','#7fd6b2','#ffd56a'];
  if ($('statisticsReadingLegend')) $('statisticsReadingLegend').innerHTML = groups.map((f,i)=>`<span><i style="--legend-color:${colors[i]}"></i>${f}</span>`).join('');
  const host = $('statisticsReadingActivity');
  if (!host) return;
  const buckets = chartBuckets(events, e => normalizeReadingKindGroup(e.kind), groups);
  host.innerHTML = lineChartSvg(buckets.labels, buckets.series, colors, 'No reading activity yet.', { yLabel: 'Reads', xLabel: 'Month' });
}
function normalizeReadingKindGroup(kind) {
  const k = String(kind || '').trim().toLowerCase();
  if (k === 'manual') return 'Manual';
  if (k === 'strategy guide' || k === 'guide') return 'Strategy Guide';
  if (k === 'magazine') return 'Magazine';
  return '';
}
function chartBuckets(events, groupFn, groupKeys = []) {
  const labels = [];
  const now = new Date();
  for (let i = 11; i >= 0; i--) {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
    labels.push(`${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}`);
  }
  const keys = groupKeys.length ? groupKeys : ['Items'];
  const series = Object.fromEntries(keys.map(key => [key, Array(12).fill(0)]));
  events.forEach(e => {
    const d = new Date(e.at || '');
    if (Number.isNaN(d.getTime())) return;
    const label = `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}`;
    const idx = labels.indexOf(label);
    const key = groupFn(e);
    if (idx >= 0 && key && series[key]) series[key][idx] += 1;
  });
  return { labels, series };
}
function statisticsMonthLabel(label, index = 0) {
  const [year, month] = String(label || '').split('-');
  const date = new Date(Number(year) || new Date().getFullYear(), Math.max(0, (Number(month) || 1) - 1), 1);
  const text = date.toLocaleDateString(undefined, { month: 'short' });
  return index === 0 || month === '01' ? `${text} ${String(year || '').slice(-2)}` : text;
}
function lineChartSvg(labels, seriesMap, colors, emptyText, options = {}) {
  const keys = Object.keys(seriesMap || {});
  const rawMax = Math.max(0, ...keys.flatMap(k => seriesMap[k] || [0]));
  const max = Math.max(1, rawMax);
  const w = 920, h = 286, padLeft = 58, padRight = 28, padTop = 28, padBottom = 44;
  const plotW = w - padLeft - padRight;
  const plotH = h - padTop - padBottom;
  const hasData = keys.some(k => (seriesMap[k] || []).some(v => v > 0));
  if (!hasData) return `<div class="statistics-empty-chart">${escapeHtml(emptyText || 'No chart data yet.')}</div>`;
  const ticks = [0, .25, .5, .75, 1];
  const grid = ticks.map(t => {
    const y = padTop + plotH * (1 - t);
    const value = Math.round(max * t);
    return `<line x1="${padLeft}" y1="${y.toFixed(1)}" x2="${w-padRight}" y2="${y.toFixed(1)}" class="statistics-grid-line"/><text class="statistics-axis-label" x="${padLeft-10}" y="${(y+4).toFixed(1)}" text-anchor="end">${escapeHtml(String(value))}</text>`;
  }).join('');
  const paths = keys.map((key, idx) => {
    const vals = seriesMap[key] || [];
    const points = vals.map((v,i) => {
      const x = padLeft + plotW * (i / Math.max(1, vals.length-1));
      const y = padTop + plotH * (1 - (v / max));
      return [x,y];
    });
    const d = points.map((p,i)=>`${i?'L':'M'}${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' ');
    const circles = points.map(p=>`<circle cx="${p[0].toFixed(1)}" cy="${p[1].toFixed(1)}" r="3" fill="${colors[idx % colors.length]}"/>`).join('');
    return `<path d="${d}" fill="none" stroke="${colors[idx % colors.length]}" stroke-width="3"/>${circles}`;
  }).join('');
  const xLabels = labels.map((label,i) => { const x = padLeft + plotW*(i/Math.max(1, labels.length-1)); return `<text class="statistics-axis-label" x="${x.toFixed(1)}" y="${h-18}" text-anchor="middle">${escapeHtml(statisticsMonthLabel(label, i))}</text>`; }).join('');
  const yCaption = options.yLabel ? `<text class="statistics-axis-caption" x="14" y="${padTop + plotH/2}" text-anchor="middle" transform="rotate(-90 14 ${padTop + plotH/2})">${escapeHtml(options.yLabel)}</text>` : '';
  const xCaption = options.xLabel ? `<text class="statistics-axis-caption" x="${padLeft + plotW/2}" y="${h-2}" text-anchor="middle">${escapeHtml(options.xLabel)}</text>` : '';
  return `<svg class="statistics-chart-svg" viewBox="0 0 ${w} ${h}" role="img" aria-label="Statistics line chart">${grid}${paths}${xLabels}${yCaption}${xCaption}</svg>`;
}
function renderStatisticsManagement(items) {
  const monthCounts = new Map();
  (items || []).forEach(item => {
    const d = new Date(item.modified || item.Modified || item.addedAt || item.createdAt || '');
    if (Number.isNaN(d.getTime())) return;
    const key = `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}`;
    monthCounts.set(key, (monthCounts.get(key) || 0) + 1);
  });
  const labels = [];
  const now = new Date();
  for (let i = 11; i >= 0; i--) {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
    labels.push(`${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}`);
  }
  const series = { 'Files Added': labels.map(label => monthCounts.get(label) || 0) };
  if ($('statisticsFilesTimelineLegend')) $('statisticsFilesTimelineLegend').innerHTML = '<span><i style="--legend-color:#6aa6ff"></i>Files Added</span>';
  if ($('statisticsFilesTimeline')) $('statisticsFilesTimeline').innerHTML = lineChartSvg(labels, series, ['#6aa6ff'], 'No dated file activity found yet.', { yLabel: 'Files Added', xLabel: 'Month Added' });
  const formats = countFormatStats(items);
  if ($('statisticsFormatTableBody')) $('statisticsFormatTableBody').innerHTML = formats.map(row => `<tr><td>${escapeHtml(row.ext)}</td><td>${escapeHtml(row.format)}</td><td>${escapeHtml(fmtBytes(row.size))}</td><td>${escapeHtml(String(row.count))}</td></tr>`).join('') || '<tr><td colspan="4">No format data yet.</td></tr>';
  if ($('statisticsFormatGraph')) $('statisticsFormatGraph').innerHTML = formats.map(row => {
    const max = Math.max(1, ...formats.map(f => f.size));
    const pct = Math.max(4, Math.round((row.size / max) * 100));
    return `<div class="statistics-format-bar"><span>${escapeHtml(row.ext)}</span><b style="width:${pct}%"></b><em>${escapeHtml(fmtBytes(row.size))} / ${row.count} files</em></div>`;
  }).join('') || '<p class="sub">No format data yet.</p>';
}
function countFormatStats(items) {
  const map = new Map();
  (items || []).forEach(item => {
    const ext = fileExtensionOf(item);
    const format = String(item.format || item.Format || (ext === '.pdf' ? 'PDF' : ext === '.epub' ? 'EPUB' : 'Archive')).toUpperCase();
    const existing = map.get(ext) || { ext, format, count: 0, size: 0 };
    existing.count += 1;
    existing.size += Number(item.sizeBytes || item.SizeBytes || 0);
    map.set(ext, existing);
  });
  return [...map.values()].sort((a,b)=>b.size-a.size || b.count-a.count || a.ext.localeCompare(b.ext));
}
function setStatisticsTab(tab) {
  state.statistics.activeTab = tab === 'management' ? 'management' : 'stats';
  document.querySelectorAll('.statistics-tab').forEach(btn => btn.classList.toggle('active', (btn.dataset.statisticsTab || 'stats') === state.statistics.activeTab));
  $('statisticsStatsPanel')?.classList.toggle('hidden', state.statistics.activeTab !== 'stats');
  $('statisticsManagementPanel')?.classList.toggle('hidden', state.statistics.activeTab !== 'management');
  renderStatistics();
}
function updateSettingsInsights() {
  if ($('settingsInsightTotal')) $('settingsInsightTotal').textContent = String(state.items.length || 0);
  if ($('settingsInsightManuals')) $('settingsInsightManuals').textContent = String(count('Manual'));
  if ($('settingsInsightGuides')) $('settingsInsightGuides').textContent = String(count('Strategy Guide'));
  if ($('settingsInsightMags')) $('settingsInsightMags').textContent = String(count('Magazine'));
}

function showLibraryScreen() {
  cleanupInactiveViewsForNavigation('library');
  clearColorscapeDetailTheme();
  document.body.classList.remove('settings-sidebar-mode', 'reader-page-mode', 'profile-page-mode');
  document.body.classList.remove('detail-page-mode');
  hideAppView('settingsView');
  hideAppView('profileView');
  hideAppView('readerView');
  hideAppView('detailView');
  showAppView('libraryView');
}
function showSettingsScreen(tab = 'account') {
  cleanupInactiveViewsForNavigation('settings');
  clearColorscapeDetailTheme();
  document.body.classList.remove('detail-page-mode', 'reader-page-mode', 'profile-page-mode');
  document.body.classList.add('settings-sidebar-mode');
  hideAppView('readerView');
  hideAppView('detailView');
  hideAppView('libraryView');
  showAppView('settingsView');
  hideAppView('profileView');
  activateSettingsTab(tab);
  updateSettingsInsights();
}
function activateSettingsTab(tab = 'account') {
  if (tab === 'insights') tab = 'statistics';
  const allowed = new Set(['account', 'preferences', 'keybinds', 'reading-profiles', 'customize', 'opds', 'devices', 'insights-devices', 'statistics', 'server', 'metadata-manager', 'library', 'media', 'email', 'users', 'tasks', 'info', 'email-history']);
  const active = allowed.has(tab) ? tab : 'account';
  document.querySelectorAll('.settings-nav, .settings-subnav').forEach(btn => {
    const tab = btn.dataset.settingsTab || '';
    const isInsightsParent = btn.classList.contains('settings-nav') && tab === 'insights' && (active.startsWith('insights') || active === 'statistics');
    const isAccountParent = btn.classList.contains('settings-nav') && tab === 'account' && ['account', 'preferences', 'keybinds', 'reading-profiles', 'customize', 'devices'].includes(active);
    const isInfoParent = btn.classList.contains('settings-nav') && tab === 'info' && ['info','email-history'].includes(active);
    const isServerParent = btn.classList.contains('settings-nav') && tab === 'server' && ['server', 'metadata-manager', 'library', 'opds', 'media', 'email', 'users', 'tasks'].includes(active);
    btn.classList.toggle('active', tab === active || isInsightsParent || isAccountParent || isInfoParent || isServerParent);
  });
  if ($('settingsAccountPanel')) $('settingsAccountPanel').classList.toggle('hidden', active !== 'account');
  if ($('settingsPreferencesPanel')) $('settingsPreferencesPanel').classList.toggle('hidden', active !== 'preferences');
  if ($('settingsKeybindsPanel')) $('settingsKeybindsPanel').classList.toggle('hidden', active !== 'keybinds');
  if ($('settingsInsightsPanel')) $('settingsInsightsPanel').classList.toggle('hidden', active !== 'insights');
  if ($('settingsInsightDevicesPanel')) $('settingsInsightDevicesPanel').classList.toggle('hidden', active !== 'insights-devices');
  if ($('settingsStatisticsPanel')) $('settingsStatisticsPanel').classList.toggle('hidden', active !== 'statistics');
  if ($('settingsReadingProfilesPanel')) $('settingsReadingProfilesPanel').classList.toggle('hidden', active !== 'reading-profiles');
  if ($('settingsCustomizePanel')) $('settingsCustomizePanel').classList.toggle('hidden', active !== 'customize');
  if ($('settingsOpdsPanel')) $('settingsOpdsPanel').classList.toggle('hidden', active !== 'opds');
  if ($('settingsDevicesPanel')) $('settingsDevicesPanel').classList.toggle('hidden', active !== 'devices');
  if ($('settingsServerPanel')) $('settingsServerPanel').classList.toggle('hidden', active !== 'server');
  if ($('settingsMediaPanel')) $('settingsMediaPanel').classList.toggle('hidden', active !== 'media');
  if ($('settingsEmailPanel')) $('settingsEmailPanel').classList.toggle('hidden', active !== 'email');
  if ($('settingsUsersPanel')) $('settingsUsersPanel').classList.toggle('hidden', active !== 'users');
  if ($('settingsTasksPanel')) $('settingsTasksPanel').classList.toggle('hidden', active !== 'tasks');
  if ($('settingsMetadataManagerPanel')) $('settingsMetadataManagerPanel').classList.toggle('hidden', active !== 'metadata-manager');
  if ($('settingsImportPanel')) $('settingsImportPanel').classList.toggle('hidden', active !== 'library');
  if ($('settingsInfoPanel')) $('settingsInfoPanel').classList.toggle('hidden', active !== 'info');
  if ($('settingsEmailHistoryPanel')) $('settingsEmailHistoryPanel').classList.toggle('hidden', active !== 'email-history');
  if (active === 'preferences') renderPreferencesSettings();
  if (active === 'keybinds') renderKeybindsSettings();
  if (active === 'customize') renderCustomizeSettings();
  if (active === 'server') loadServerSettings(false);
  if (active === 'media') loadServerSettings(false);
  if (active === 'email') { if (!state.serverSettings) loadServerSettings(false); loadEmailSettings(false); requestAnimationFrame(syncEmailTemplatePreview); }
  if (active === 'users') loadUsersSettings(false);
  if (active === 'tasks') loadTaskSettings(false);
  if (active === 'statistics') renderStatistics();
  if (active === 'info') { loadSystemInfo(false); loadSystemPerformance(); checkStableUpdates(false); }
  if (active === 'email-history') loadEmailHistory(false);
  if (active === 'reading-profiles') renderReadingProfileSettings();
  if (active === 'opds') { renderOpdsSettings(); syncOpdsSettingsFromServer(false); }
  if (active === 'devices' || active === 'insights-devices') { renderDeviceHistory(); sendDeviceHeartbeat({ refresh: true }); loadDeviceHistory(false); }
  if (active === 'metadata-manager') renderMetadataManager();
  const libraryMode = active === 'library';
  if ($('settingsImportTitle')) $('settingsImportTitle').textContent = 'Library';
  if ($('settingsImportSub')) $('settingsImportSub').textContent = 'Manage stored library paths, rescan individual folders, and edit scan-in-place library entries.';
}


setupHomebarIconFallbacks();
loadCategoryStructure();
loadLibraryCoverScale();
if ($('leftToggle')) $('leftToggle').addEventListener('click', () => runPanelTransition(() => document.body.classList.toggle('left-collapsed')));
if ($('rightToggleTop')) $('rightToggleTop').addEventListener('click', () => toggleRightPanel());
if ($('rightToggle')) $('rightToggle').addEventListener('click', () => toggleRightPanel(false));
if ($('search')) $('search').addEventListener('input', applyFilters);
if ($('sort')) $('sort').addEventListener('change', applyFilters);
if ($('coverSizeSlider')) $('coverSizeSlider').addEventListener('input', e => setLibraryCoverScale(e.currentTarget.value));
if ($('categoryStructureSelect')) $('categoryStructureSelect').addEventListener('change', e => { saveCategoryStructure(e.currentTarget.value); state.categoryFilter = ''; state.customFilter = null; state.filter = 'All Content'; state.viewMode = 'all'; updateNavActive(); applyFilters(); });
document.querySelectorAll('.nav').forEach(btn => btn.addEventListener('click', () => {
  showLibraryScreen();
  state.viewMode = btn.dataset.view || 'all';
  state.filter = btn.dataset.filter || 'All Content';
  state.categoryFilter = '';
  state.customFilter = null;
  if ($('search')) $('search').value = '';
  document.querySelectorAll('.chip').forEach(c => c.classList.toggle('active', c.dataset.kind === state.filter || (state.filter === 'All Content' && c.dataset.kind === 'All Content')));
  updateNavActive();
  scrollMainToTop();
  applyFilters();
}));
document.querySelectorAll('.chip').forEach(btn => btn.addEventListener('click', () => {
  showLibraryScreen();
  state.filter = btn.dataset.kind;
  state.viewMode = state.filter === 'All Content' ? 'all' : state.filter === 'Manual' ? 'manuals' : state.filter === 'Strategy Guide' ? 'strategy-guides' : 'magazines';
  state.categoryFilter = '';
  state.customFilter = null;
  document.querySelectorAll('.chip').forEach(c => c.classList.toggle('active', c === btn));
  updateNavActive();
  scrollMainToTop();
  applyFilters();
}));
document.querySelectorAll('.collection').forEach(btn => btn.addEventListener('click', () => {
  showLibraryScreen();
  const kind = btn.dataset.kind || 'All Content';
  state.filter = kind;
  state.viewMode = kind === 'Manual' ? 'manual-systems' : kind === 'Strategy Guide' ? 'guide-systems' : kind === 'Magazine' ? 'magazine-series' : 'all';
  state.categoryFilter = '';
  state.customFilter = null;
  if ($('search')) $('search').value = '';
  document.querySelectorAll('.chip').forEach(c => c.classList.toggle('active', c.dataset.kind === kind));
  updateNavActive();
  scrollMainToTop();
  applyFilters();
}));
async function openSelectedReaderFromUi(e = null) {
  if (e) {
    e.preventDefault();
    e.stopPropagation?.();
    e.stopImmediatePropagation?.();
  }
  const selectedId = state.selected?.id || $('readBtn')?.dataset?.itemId || document.querySelector('.card.selected')?.dataset?.id || '';
  const item = state.selected || (state.items || []).find(i => String(i.id) === String(selectedId));
  if (!item) {
    alert('Select an item before opening the reader.');
    return;
  }
  try {
    await openReader(item);
  } catch (err) {
    console.error('Open reader failed', err);
    alert(`Unable to open reader: ${err?.message || err}`);
  }
}
if ($('readBtn')) $('readBtn').addEventListener('click', openSelectedReaderFromUi, true);
document.addEventListener('click', e => {
  const btn = e.target.closest?.('#readBtn');
  if (!btn) return;
  openSelectedReaderFromUi(e);
}, true);
if ($('downloadBtn')) $('downloadBtn').addEventListener('click', e => { e.preventDefault(); if (state.selected) window.open(`/api/items/${state.selected.id}/file`, '_blank'); });
if ($('lookupPlatformsBtn')) $('lookupPlatformsBtn').addEventListener('click', e => { e.preventDefault(); resolveStrategyPlatforms(); });
if ($('deleteBtn')) $('deleteBtn').addEventListener('click', async e => {
  e.preventDefault();
  if (!state.selected) return;
  const item = state.selected;
  const itemId = String(item.id || item.Id || '').trim();
  const confirmed = await showAppConfirm({
    title: 'Remove item?',
    message: `Remove "${displayTitle(item)}" from Guidevault? The source file will stay where it is; Guidevault will only untrack it.`,
    okText: 'Remove Item',
    danger: true
  });
  if (!confirmed || !itemId) return;

  // Remove from the visible library immediately. The server request only persists
  // the untracked marker; the user should not wait on a rescan/cache write.
  state.items = (state.items || []).filter(i => String(i.id || i.Id || '') !== itemId);
  state.filtered = (state.filtered || []).filter(i => String(i.id || i.Id || '') !== itemId);
  state.selected = null;
  showLibraryScreen();
  applyFilters();
  installGlobalDetailDelegate();

  const taskId = upsertLibraryTask({
    title: 'Remove from Library',
    message: `"${displayTitle(item)}" removed from the visible library. It will be rediscovered on the next rescan.`,
    progress: 80,
    kind: 'library-removal'
  });

  fetch(`/api/items/${encodeURIComponent(itemId)}`, { method: 'DELETE', cache: 'no-store' })
    .then(async res => {
      if (!res.ok) {
        let msg = 'Unable to remove item from Guidevault.';
        try { msg = (await res.json()).error || msg; } catch {}
        throw new Error(msg);
      }
      updateLibraryTask(taskId, 'Item removed from the current index. Rescan will rediscover the source file.', 100, 'completed', 'Remove from Library');
    })
    .catch(err => {
      console.error('Remove from library failed', err);
      updateLibraryTask(taskId, `Remove failed: ${err?.message || err}`, 100, 'failed', 'Remove from Library');
      alert(`Unable to remove item from Guidevault: ${err?.message || err}`);
      // Put the item back locally if the server did not accept the removal.
      if (!state.items.some(i => String(i.id || i.Id || '') === itemId)) {
        state.items.push(item);
        applyFilters();
      }
    });
});
if ($('closeReader')) $('closeReader').addEventListener('click', () => { setReaderOverlayVisible(false); $('readerView').classList.add('hidden'); $('libraryView').classList.remove('hidden'); });
if ($('nextPage')) $('nextPage').addEventListener('click', () => showPage(state.reader.index, 'next'));
if ($('prevPage')) $('prevPage').addEventListener('click', () => showPage(state.reader.index, 'prev'));
if ($('rightHit')) $('rightHit').addEventListener('click', e => { if (consumeReaderLongPressClick()) { e.preventDefault(); return; } showPage(state.reader.index, 'next'); });
if ($('leftHit')) $('leftHit').addEventListener('click', e => { if (consumeReaderLongPressClick()) { e.preventDefault(); return; } showPage(state.reader.index, 'prev'); });
if ($('centerHit')) $('centerHit').addEventListener('click', e => { e.preventDefault(); if (consumeReaderLongPressClick()) return; toggleReaderOverlay(); });
if ($('readerModeOne')) $('readerModeOne').addEventListener('click', e => { e.preventDefault(); setReaderDisplayMode(1); setReaderOverlayVisible(true); });
if ($('readerModeTwo')) $('readerModeTwo').addEventListener('click', e => { e.preventDefault(); setReaderDisplayMode(2); setReaderOverlayVisible(true); });
if ($('readerDisplayModeSelect')) $('readerDisplayModeSelect').addEventListener('change', e => { setReaderDisplayMode(Number(e.target.value)); setReaderOverlayVisible(true); });
if ($('readerZoomOut')) $('readerZoomOut').addEventListener('click', e => { e.preventDefault(); adjustReaderZoom(-10); });
if ($('readerZoomIn')) $('readerZoomIn').addEventListener('click', e => { e.preventDefault(); adjustReaderZoom(10); });
if ($('readerTransitionSelect')) $('readerTransitionSelect').addEventListener('change', e => { setReaderTransitionMode(e.target.value); setReaderOverlayVisible(true); });
if ($('readerBackgroundSelect')) $('readerBackgroundSelect').addEventListener('change', e => { setReaderBackground(e.target.value); setReaderOverlayVisible(true); });
if ($('readerBackgroundBrightness')) $('readerBackgroundBrightness').addEventListener('input', e => { setReaderBackgroundBrightness(e.target.value); setReaderOverlayVisible(true); });
if ($('readerMagnifierToggle')) $('readerMagnifierToggle').addEventListener('click', e => { e.preventDefault(); toggleReaderMagnifier(e); setReaderOverlayVisible(true); });
if ($('readerMagnifierMenu')) $('readerMagnifierMenu').addEventListener('click', e => { e.preventDefault(); setReaderMagnifierPanelVisible(!state.reader.magnifierSettingsVisible); setReaderOverlayVisible(true); });
if ($('readerMagnifierWidth')) $('readerMagnifierWidth').addEventListener('input', e => updateReaderMagnifierSetting('width', e.target.value));
if ($('readerMagnifierHeight')) $('readerMagnifierHeight').addEventListener('input', e => updateReaderMagnifierSetting('height', e.target.value));
if ($('readerMagnifierOpacity')) $('readerMagnifierOpacity').addEventListener('input', e => updateReaderMagnifierSetting('opacity', e.target.value));
if ($('readerMagnifierZoom')) $('readerMagnifierZoom').addEventListener('input', e => updateReaderMagnifierSetting('zoom', e.target.value));
if ($('readerMagnifierLongClick')) $('readerMagnifierLongClick').addEventListener('change', e => updateReaderMagnifierSetting('longClickEnabled', e.target.checked));
['readerStage','book','leftHit','centerHit','rightHit'].forEach(id => {
  const el = $(id);
  if (!el) return;
  if (id === 'leftHit' || id === 'centerHit' || id === 'rightHit') {
    el.setAttribute('tabindex', '-1');
    el.setAttribute('aria-hidden', 'true');
    el.removeAttribute('role');
  }
  el.addEventListener('pointermove', handleReaderPointerMove);
  el.addEventListener('pointerdown', beginReaderLongPress);
  el.addEventListener('pointerup', cancelReaderLongPress);
  el.addEventListener('pointerleave', cancelReaderLongPress);
  el.addEventListener('pointercancel', cancelReaderLongPress);
});
if ($('readerBookmarkPage')) $('readerBookmarkPage').addEventListener('click', e => { e.preventDefault(); bookmarkCurrentReaderPage(); setReaderOverlayVisible(true); });
if ($('readerAdvancedSettings')) $('readerAdvancedSettings').addEventListener('click', e => { e.preventDefault(); toggleReaderAdvancedSettings(); setReaderOverlayVisible(true); });
if ($('readerExitFullscreen')) $('readerExitFullscreen').addEventListener('click', e => { e.preventDefault(); exitReaderFullscreenOnly(); setReaderOverlayVisible(true); });
if ($('readerExitLibrary')) $('readerExitLibrary').addEventListener('click', e => { e.preventDefault(); exitReaderToLibrary(); });
if ($('readerExitDetails')) $('readerExitDetails').addEventListener('click', e => { e.preventDefault(); exitReaderToDetails(); });
if ($('readerPageSlider')) {
  $('readerPageSlider').addEventListener('input', e => { state.reader.scrubbing = true; setReaderSliderPreview(e.target.value); });
  $('readerPageSlider').addEventListener('change', e => { state.reader.scrubbing = false; jumpReaderToPage(e.target.value); });
  $('readerPageSlider').addEventListener('pointerup', e => { if (state.reader.scrubbing) { state.reader.scrubbing = false; jumpReaderToPage(e.target.value); } });
  $('readerPageSlider').addEventListener('keyup', e => { if (e.key === 'Enter') { state.reader.scrubbing = false; jumpReaderToPage(e.target.value); } });
}
if ($('readerBindingShadeEnabled')) $('readerBindingShadeEnabled').addEventListener('change', e => { updateReaderShadingSetting('bindingEnabled', e.target.checked); setReaderOverlayVisible(true); });
if ($('readerBindingShadeSlider')) $('readerBindingShadeSlider').addEventListener('input', e => { updateReaderShadingSetting('bindingDepth', e.target.value); setReaderOverlayVisible(true); });
if ($('readerOuterShadeEnabled')) $('readerOuterShadeEnabled').addEventListener('change', e => { updateReaderShadingSetting('outerEnabled', e.target.checked); setReaderOverlayVisible(true); });
if ($('readerOuterShadeSlider')) $('readerOuterShadeSlider').addEventListener('input', e => { updateReaderShadingSetting('outerDepth', e.target.value); setReaderOverlayVisible(true); });

['pageLeftImage','pageRightImage'].forEach(id => {
  const image = $(id);
  if (image) image.addEventListener('load', () => { scheduleReaderPageEdgeShadingBounds(); requestAnimationFrame(updateReaderPageStackColors); });
});
document.addEventListener('keydown', e => {
  if (e.key !== 'Escape' || !state.reader.overlayVisible) return;
  if (state.reader.advancedVisible) { setReaderAdvancedSettingsVisible(false); return; }
  setReaderOverlayVisible(false);
});
if ($('readerFullscreen')) $('readerFullscreen').addEventListener('click', async () => {
  const el = $('readerStage');
  try {
    if (!document.fullscreenElement) await el.requestFullscreen?.(); else await document.exitFullscreen?.();
  } catch {}
  updateReaderFullscreenUi();
});

document.addEventListener('click', handleFavoriteClick, true);
if ($('userMenuBtn')) $('userMenuBtn').addEventListener('click', e => { e.preventDefault(); e.stopPropagation(); toggleUserMenu(); });
if ($('userMenuProfile')) $('userMenuProfile').addEventListener('click', e => { e.preventDefault(); e.stopPropagation(); setUserMenuOpen(false); showUserProfilePage(); });
document.addEventListener('click', e => {
  const profileButton = e.target.closest?.('#userMenuProfile,[data-open-profile-page]');
  if (!profileButton) return;
  e.preventDefault();
  e.stopPropagation();
  setUserMenuOpen(false);
  showUserProfilePage();
}, true);
window.addEventListener('hashchange', () => {
  if (window.location.hash === '#profile') showUserProfilePage({ skipHash: true });
});
if ($('userMenuHelp')) $('userMenuHelp').addEventListener('click', e => { e.preventDefault(); setUserMenuOpen(false); showSettingsScreen('keybinds'); });
if ($('userMenuLogout')) $('userMenuLogout').addEventListener('click', e => { e.preventDefault(); setUserMenuOpen(false); logoutGuidevault(); });
document.addEventListener('click', e => { if (!$('userMenuPanel')?.classList.contains('hidden') && !e.target.closest?.('.top-user-wrap')) setUserMenuOpen(false); });
if ($('settingsBtn')) $('settingsBtn').addEventListener('click', () => showSettingsScreen('account'));
if ($('settingsBackToLibrary')) $('settingsBackToLibrary').addEventListener('click', () => showLibraryScreen());
document.addEventListener('keydown', handleReaderKeydown, true);
if ($('loginForm')) $('loginForm').addEventListener('submit', handleLoginSubmit);
if ($('accountEditLogin')) $('accountEditLogin').addEventListener('click', e => { e.preventDefault(); setAccountEditMode(true); });
if ($('accountSaveLogin')) $('accountSaveLogin').addEventListener('click', e => { e.preventDefault(); saveAccountLoginFromSettings(); });
if ($('accountCancelEdit')) $('accountCancelEdit').addEventListener('click', e => { e.preventDefault(); cancelAccountEdit(); });
if ($('accountLogout')) $('accountLogout').addEventListener('click', e => { e.preventDefault(); logoutGuidevault(); });
if ($('preferenceUseColorscape')) $('preferenceUseColorscape').addEventListener('change', e => { setUseColorscapePreference(e.currentTarget.checked); });
if ($('keybindsList')) $('keybindsList').addEventListener('click', handleKeybindAction);
if ($('keybindsResetAll')) $('keybindsResetAll').addEventListener('click', e => { e.preventDefault(); resetAllKeybinds(); });
if ($('customizeAddShelf')) $('customizeAddShelf').addEventListener('click', e => { e.preventDefault(); addCustomizeShelf(); });
if ($('customizeShelfList')) $('customizeShelfList').addEventListener('click', handleCustomizeShelfAction);
document.querySelectorAll('.customize-tab').forEach(btn => btn.addEventListener('click', handleCustomizeTabClick));
if ($('statisticsRange')) $('statisticsRange').addEventListener('change', e => { state.statistics.range = e.target.value || 'all'; renderStatistics(); });
document.querySelectorAll('.statistics-tab').forEach(btn => btn.addEventListener('click', () => setStatisticsTab(btn.dataset.statisticsTab || 'stats')));
if ($('statisticsRefresh')) $('statisticsRefresh').addEventListener('click', e => { e.preventDefault(); renderStatistics(); });
if ($('profileRange')) $('profileRange').addEventListener('change', e => { state.profilePage.range = e.target.value || 'all'; renderPersonalProfile(); });
document.querySelectorAll('.profile-tab').forEach(btn => btn.addEventListener('click', () => setProfileTab(btn.dataset.profileTab || 'overview')));
if ($('opdsEditUrl')) $('opdsEditUrl').addEventListener('click', e => { e.preventDefault(); beginOpdsUrlEdit(); });
if ($('opdsSaveUrl')) $('opdsSaveUrl').addEventListener('click', e => { e.preventDefault(); saveOpdsUrl(); });
if ($('opdsCancelUrl')) $('opdsCancelUrl').addEventListener('click', e => { e.preventDefault(); cancelOpdsUrlEdit(); });
if ($('opdsRevealUrl')) $('opdsRevealUrl').addEventListener('click', e => { e.preventDefault(); toggleOpdsRevealUrl(); });
if ($('opdsCopyUrl')) $('opdsCopyUrl').addEventListener('click', e => { e.preventDefault(); copyTextToClipboard(buildOpdsClientUrl(), 'Copied OPDS URL.'); });
if ($('opdsUrlKeySelect')) $('opdsUrlKeySelect').addEventListener('change', handleOpdsKeySelection);
if ($('opdsGenerateKey')) $('opdsGenerateKey').addEventListener('click', e => { e.preventDefault(); beginOpdsNewKey(); });
if ($('opdsCreateKey')) $('opdsCreateKey').addEventListener('click', e => { e.preventDefault(); createOpdsKey(); });
if ($('opdsCancelNewKey')) $('opdsCancelNewKey').addEventListener('click', e => { e.preventDefault(); cancelOpdsNewKey(); });
if ($('opdsKeyTableBody')) $('opdsKeyTableBody').addEventListener('click', handleOpdsTableAction);
if ($('folderBrowseClose')) $('folderBrowseClose').addEventListener('click', e => { e.preventDefault(); closeFolderBrowser(); });
if ($('folderBrowseCancel')) $('folderBrowseCancel').addEventListener('click', e => { e.preventDefault(); closeFolderBrowser(); });
if ($('folderBrowseUse')) $('folderBrowseUse').addEventListener('click', e => { e.preventDefault(); useSelectedFolderBrowserPath(); });
if ($('folderBrowseList')) $('folderBrowseList').addEventListener('click', e => { const btn = e.target.closest?.('[data-folder-path]'); if (btn) { e.preventDefault(); loadFolderBrowserPath(btn.dataset.folderPath || ''); } });
if ($('folderBrowseQuickRoots')) $('folderBrowseQuickRoots').addEventListener('click', e => { const btn = e.target.closest?.('[data-folder-path]'); if (btn) { e.preventDefault(); loadFolderBrowserPath(btn.dataset.folderPath || ''); } });
if ($('readingProfilePresetSelect')) $('readingProfilePresetSelect').addEventListener('change', () => loadSelectedReadingProfilePresetForm());
if ($('readingProfilePresetZoom')) $('readingProfilePresetZoom').addEventListener('input', refreshReadingProfilePresetZoomOutput);
if ($('readingProfileNewPreset')) $('readingProfileNewPreset').addEventListener('click', e => { e.preventDefault(); createReadingProfilePreset(); });
if ($('readingProfileSavePreset')) $('readingProfileSavePreset').addEventListener('click', e => { e.preventDefault(); saveReadingProfilePreset(); });
if ($('readingProfileDeletePreset')) $('readingProfileDeletePreset').addEventListener('click', e => { e.preventDefault(); deleteReadingProfilePreset(); });
if ($('readingProfileSetDefaultPreset')) $('readingProfileSetDefaultPreset').addEventListener('click', e => { e.preventDefault(); setDefaultReadingProfilePreset(); });
if ($('readingProfilePresetList')) $('readingProfilePresetList').addEventListener('click', e => {
  const row = e.target.closest?.('.reading-profile-preset-row');
  if (!row) return;
  e.preventDefault();
  const id = row.dataset.profileId || '';
  if ($('readingProfilePresetSelect')) $('readingProfilePresetSelect').value = id;
  loadSelectedReadingProfilePresetForm();
});
if ($('detailGroupProfileSelect')) $('detailGroupProfileSelect').addEventListener('change', () => updateDetailReadingProfileEffectivePreview());
if ($('detailEntryProfileSelect')) $('detailEntryProfileSelect').addEventListener('change', () => updateDetailReadingProfileEffectivePreview());
if ($('detailSaveGroupProfile')) $('detailSaveGroupProfile').addEventListener('click', e => { e.preventDefault(); saveDetailReadingProfileAssignment('group'); });
if ($('detailSaveEntryProfile')) $('detailSaveEntryProfile').addEventListener('click', e => { e.preventDefault(); saveDetailReadingProfileAssignment('entry'); });
if ($('detailClearGroupProfile')) $('detailClearGroupProfile').addEventListener('click', e => { e.preventDefault(); clearDetailReadingProfileAssignment('group'); });
if ($('detailClearEntryProfile')) $('detailClearEntryProfile').addEventListener('click', e => { e.preventDefault(); clearDetailReadingProfileAssignment('entry'); });
if ($('detailReadingProfileManagePresets')) $('detailReadingProfileManagePresets').addEventListener('click', e => { e.preventDefault(); showSettingsScreen('reading-profiles'); });
if ($('taskMonitorBtn')) $('taskMonitorBtn').addEventListener('click', e => { e.preventDefault(); setTaskPanelVisible(!state.taskPanelVisible); pollTasks(false); });
if ($('updateNotifyBtn')) $('updateNotifyBtn').addEventListener('click', e => { e.preventDefault(); showSettingsScreen('info'); setSystemInfoStatus('A stable container image update is available. Pull the new image from your Docker host when ready.', 'success'); });
if ($('systemCheckUpdates')) $('systemCheckUpdates').addEventListener('click', async e => { e.preventDefault(); setSystemInfoStatus('Checking stable update feed...', 'info'); await checkStableUpdates(true); setSystemInfoStatus(state.updateCheck?.message || 'Update check complete.', state.updateCheck?.updateAvailable ? 'success' : ''); });
if ($('systemTrimMemory')) $('systemTrimMemory').addEventListener('click', e => { e.preventDefault(); trimGuidevaultMemory(); });
document.addEventListener('click', e => {
  const clear = e.target.closest?.('#taskClearBtn');
  if (clear) { e.preventDefault(); clearTaskMessages(); return; }
  if (!$('taskMonitorPanel') || !$('taskMonitorBtn')) return;
  if (!$('taskMonitorPanel').contains(e.target) && !$('taskMonitorBtn').contains(e.target)) setTaskPanelVisible(false);
});
if ($('librarySettingsBtn')) $('librarySettingsBtn').addEventListener('click', () => $('libraryDialog').showModal());
if ($('editKind')) $('editKind').addEventListener('change', e => {
  updateTypedMetadataFieldVisibility(e.target.value);
  syncPreferredPlatformEditorState();
});
if ($('editAssociatedPlatforms')) $('editAssociatedPlatforms').addEventListener('input', syncPreferredPlatformEditorState);
initMetadataMultiSelectControls();
document.addEventListener('click', e => {
  const button = e.target.closest?.('.meta-multi-button');
  if (button) {
    e.preventDefault();
    const shell = button.closest('.meta-multi-select');
    const willOpen = !shell.classList.contains('open');
    closeMetadataMultiSelects(willOpen ? shell : null);
    shell.classList.toggle('open', willOpen);
    button.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
    return;
  }
  if (!e.target.closest?.('.meta-multi-select')) closeMetadataMultiSelects();
});
document.addEventListener('change', e => {
  const input = e.target.closest?.('.meta-multi-option-input');
  if (!input) return;
  const shell = input.closest('.meta-multi-select');
  const id = shell?.dataset.multiSelect || '';
  const select = $(id);
  if (!select) return;
  const value = String(input.dataset.value || '').trim().toLowerCase();
  [...select.options].forEach(option => {
    if (optionValueOf(option).toLowerCase() === value) option.selected = input.checked;
  });
  syncMetadataMultiSelectControl(id);
  if (id === 'editEditionType') updateEditionControls();
});
document.addEventListener('keydown', e => {
  if (e.key === 'Escape') closeMetadataMultiSelects();
});
if ($('editEditionType')) $('editEditionType').addEventListener('change', updateEditionControls);
if ($('saveMetadataBtn')) $('saveMetadataBtn').addEventListener('click', async e => { e.preventDefault(); await saveSelectedMetadata({}, { tab: 'metadata', button: e.currentTarget }); });
if ($('saveNotesBtn')) $('saveNotesBtn').addEventListener('click', async e => { e.preventDefault(); await saveSelectedMetadata({ notes: $('notesText').value }, { tab: 'notes', button: e.currentTarget }); });
if ($('detailPrevBtn')) $('detailPrevBtn').addEventListener('click', e => { e.preventDefault(); navigateDetailAdjacent('previous'); });
if ($('detailNextBtn')) $('detailNextBtn').addEventListener('click', e => { e.preventDefault(); navigateDetailAdjacent('next'); });
document.querySelectorAll('.tab').forEach(btn => btn.addEventListener('click', () => activateTab(btn.dataset.tab)));
async function saveLibraryPathFromInput(inputId, statusId, dialogId) {
  const input = $(inputId);
  const status = $(statusId);
  const libraryPath = input ? input.value.trim() : '';
  if (!libraryPath) { if (status) status.textContent = 'Choose or paste a library folder path first.'; return; }
  if (status) status.textContent = '';
  const localTaskId = upsertLibraryTask({
    title: 'Library scan',
    message: 'Saving library path and starting scan...',
    progress: 2
  });
  const res = await fetch('/api/settings/library', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({ libraryPath }) });
  let data = null;
  try { data = await res.json(); } catch {}
  if (!res.ok) {
    const msg = data?.error || 'Unable to save library path.';
    updateLibraryTask(localTaskId, msg, 100, 'failed', 'Library scan');
    if (status) status.textContent = msg;
    alert(msg);
    return;
  }
  state.libraryPath = data.libraryPath || libraryPath;
  if (dialogId) $(dialogId).close();
  const taskId = data?.taskId || data?.TaskId || '';
  if (taskId) {
    replaceLibraryTask(localTaskId, {
      id: taskId,
      kind: 'library-scan',
      title: 'Library scan',
      status: 'running',
      message: data?.message || 'Library scan started.',
      progressPercent: 5,
      updatedAt: new Date().toISOString()
    });
  }
  await syncRightToggleLabels();
  await pollTasks(true);
  installGlobalDetailDelegate();
}
if ($('saveLibraryPath')) $('saveLibraryPath').addEventListener('click', async e => { e.preventDefault(); await saveLibraryPathFromInput('libraryPathInput','libraryDialogStatus','libraryDialog'); });

if ($('deviceAddEmailTop')) $('deviceAddEmailTop').addEventListener('click', e => { e.preventDefault(); beginEmailDeviceAdd(); });
if ($('deviceSaveEmail')) $('deviceSaveEmail').addEventListener('click', e => { e.preventDefault(); saveEmailDevice(); });
if ($('deviceCancelEmail')) $('deviceCancelEmail').addEventListener('click', e => { e.preventDefault(); cancelEmailDeviceAdd(); });
if ($('deviceRefreshHistory')) $('deviceRefreshHistory').addEventListener('click', e => { e.preventDefault(); sendDeviceHeartbeat({ refresh: true }); loadDeviceHistory(true); });
if ($('insightDeviceRefresh')) $('insightDeviceRefresh').addEventListener('click', e => { e.preventDefault(); sendDeviceHeartbeat({ refresh: true }); loadDeviceHistory(true); });
if ($('deviceClearStale')) $('deviceClearStale').addEventListener('click', e => { e.preventDefault(); clearStaleClientDevices(); });
if ($('deviceEmailTableBody')) $('deviceEmailTableBody').addEventListener('click', handleDeviceEmailTableAction);
if ($('deviceClientGrid')) $('deviceClientGrid').addEventListener('click', handleDeviceClientGridAction);
if ($('deviceClientGrid')) $('deviceClientGrid').addEventListener('keydown', handleDeviceClientGridKeydown);
document.addEventListener('click', e => {
  if (!state.devices?.clientMenuId) return;
  if (e.target.closest?.('[data-client-device-id]')) return;
  state.devices.clientMenuId = '';
  if (!$('settingsDevicesPanel')?.classList.contains('hidden') || !$('settingsInsightDevicesPanel')?.classList.contains('hidden')) renderDeviceHistory();
});
document.querySelectorAll('.settings-nav, .settings-subnav').forEach(btn => btn.addEventListener('click', () => {
  const tab = btn.dataset.settingsTab || 'account';
  activateSettingsTab(tab);
  if (tab === 'import' || tab === 'library') {
    renderLibrariesTable?.();
    loadLibrarySettings?.();
  }
}));

if ($('accountUploadProfilePic')) $('accountUploadProfilePic').addEventListener('click', e => { e.preventDefault(); $('accountProfilePicInput')?.click(); });
if ($('accountProfilePicInput')) $('accountProfilePicInput').addEventListener('change', e => handleAccountProfilePicFile(e.currentTarget.files?.[0]));
if ($('accountRemoveProfilePic')) $('accountRemoveProfilePic').addEventListener('click', e => { e.preventDefault(); removeAccountProfilePic(); });
if ($('opdsEnabledToggle')) $('opdsEnabledToggle').addEventListener('change', async e => {
  state.opds = state.opds || loadOpdsSettings();
  state.opds.enabled = !!e.currentTarget.checked;
  saveOpdsSettings();
  try { await saveOpdsServerSettings({ enabled: state.opds.enabled }); setOpdsStatus(state.opds.enabled ? 'OPDS enabled.' : 'OPDS disabled.', 'success'); }
  catch (err) { console.error(err); setOpdsStatus('Unable to update OPDS enabled state on the backend.', 'error'); renderOpdsSettings(); }
});
if ($('customSideNavAdd')) $('customSideNavAdd').addEventListener('click', e => { e.preventDefault(); addCustomSideNavItem(); });
if ($('customSideNavReset')) $('customSideNavReset').addEventListener('click', e => { e.preventDefault(); resetCustomSideNavItems(); });
if ($('customSideNavList')) $('customSideNavList').addEventListener('click', handleCustomSideNavListAction);
if ($('customSideNavItems')) $('customSideNavItems').addEventListener('click', e => {
  const btn = e.target.closest?.('[data-custom-nav-id]');
  if (!btn) return;
  applyCustomSideNavItem(btn.dataset.customNavId || '');
});
if ($('serverSaveSettings')) $('serverSaveSettings').addEventListener('click', e => { e.preventDefault(); saveServerSettings('general'); });
if ($('serverResetDefaults')) $('serverResetDefaults').addEventListener('click', e => { e.preventDefault(); resetServerDefaults(); });
if ($('serverCreateBackup')) $('serverCreateBackup').addEventListener('click', e => { e.preventDefault(); createServerBackup(); });
if ($('mediaSaveSettings')) $('mediaSaveSettings').addEventListener('click', e => { e.preventDefault(); saveServerSettings('media'); });
if ($('emailSaveSettings')) $('emailSaveSettings').addEventListener('click', e => { e.preventDefault(); saveEmailSettings(); });
if ($('emailTestButton')) $('emailTestButton').addEventListener('click', e => { e.preventDefault(); testEmailSettings(); });
if ($('emailProvider')) $('emailProvider').addEventListener('change', () => { syncEmailProviderUi(); syncEmailTemplatePreview(); });
if ($('emailTemplatePreset')) $('emailTemplatePreset').addEventListener('change', handleEmailTemplatePresetChange);
['emailTemplateName','emailTemplateSubject','emailTemplateBody'].forEach(id => { if ($(id)) $(id).addEventListener('input', syncEmailTemplatePreview); });
if ($('emailTemplateUploadButton')) $('emailTemplateUploadButton').addEventListener('click', e => { e.preventDefault(); $('emailTemplateFile')?.click(); });
if ($('emailTemplateFile')) $('emailTemplateFile').addEventListener('change', e => uploadEmailTemplateFile(e.currentTarget.files?.[0]));
if ($('emailHistoryRefresh')) $('emailHistoryRefresh').addEventListener('click', e => { e.preventDefault(); loadEmailHistory(true); });
if ($('usersRefresh')) $('usersRefresh').addEventListener('click', e => { e.preventDefault(); loadUsersSettings(true); });
if ($('usersInviteButton')) $('usersInviteButton').addEventListener('click', e => { e.preventDefault(); inviteUser(); });
if ($('tasksSaveSettings')) $('tasksSaveSettings').addEventListener('click', e => { e.preventDefault(); saveTaskSettings(); });
if ($('taskRunRescan')) $('taskRunRescan').addEventListener('click', async e => { e.preventDefault(); setTasksSettingsStatus('Fast rescan queued.', 'info'); await rescanLibrary(); });
if ($('taskRunEnrich')) $('taskRunEnrich').addEventListener('click', async e => { e.preventDefault(); setTasksSettingsStatus('Metadata enrichment queued.', 'info'); await enrichLibraryMetadata(); });
if ($('taskRunCleanup')) $('taskRunCleanup').addEventListener('click', async e => { e.preventDefault(); setTasksSettingsStatus('Cleanup queued.', 'info'); await cleanupLibrary(); });
if ($('taskRunBackup')) $('taskRunBackup').addEventListener('click', e => { e.preventDefault(); createServerBackup(); });
if ($('taskRunTrim')) $('taskRunTrim').addEventListener('click', e => { e.preventDefault(); trimGuidevaultMemory(); setTasksSettingsStatus('Reading cache clear requested.', 'info'); });

if ($('metadataManagerSearch')) $('metadataManagerSearch').addEventListener('input', e => metadataManagerUpdateFilter('search', e.currentTarget.value));
if ($('metadataManagerKind')) $('metadataManagerKind').addEventListener('change', e => metadataManagerUpdateFilter('filterKind', e.currentTarget.value));
if ($('metadataManagerMissing')) $('metadataManagerMissing').addEventListener('change', e => metadataManagerUpdateFilter('missing', e.currentTarget.value));
if ($('metadataManagerCategory')) $('metadataManagerCategory').addEventListener('change', e => metadataManagerUpdateFilter('category', e.currentTarget.value));
if ($('metadataManagerRefresh')) $('metadataManagerRefresh').addEventListener('click', () => { renderMetadataManager(); metadataManagerSetStatus('Metadata grid refreshed.', 'success'); });
if ($('metadataManagerSelectAll')) $('metadataManagerSelectAll').addEventListener('click', () => metadataManagerSelectVisible(true));
if ($('metadataManagerClearSelection')) $('metadataManagerClearSelection').addEventListener('click', () => { state.metadataManager.selectedIds = []; renderMetadataManager(); });
if ($('metadataManagerApplyBatch')) $('metadataManagerApplyBatch').addEventListener('click', async () => { try { await metadataManagerApplyBatch(); } catch (err) { console.error(err); metadataManagerSetStatus(`Batch apply failed: ${err?.message || err}`, 'error'); } });
if ($('metadataManagerNormalizeSelected')) $('metadataManagerNormalizeSelected').addEventListener('click', async () => { try { await metadataManagerNormalizeSelected(); } catch (err) { console.error(err); metadataManagerSetStatus(`Normalize failed: ${err?.message || err}`, 'error'); } });
if ($('metadataManagerSaveDirty')) $('metadataManagerSaveDirty').addEventListener('click', async () => { try { await metadataManagerSaveDirtyRows(); } catch (err) { console.error(err); metadataManagerSetStatus(`Save failed: ${err?.message || err}`, 'error'); } });
if ($('metadataManagerExportCsv')) $('metadataManagerExportCsv').addEventListener('click', metadataManagerExportCsv);
if ($('metadataManagerImportJson')) $('metadataManagerImportJson').addEventListener('click', () => $('metadataManagerImportFile')?.click());
if ($('metadataManagerImportFile')) $('metadataManagerImportFile').addEventListener('change', e => metadataManagerImportJsonFile(e.currentTarget.files?.[0]));
if ($('metadataManagerScrape')) $('metadataManagerScrape').addEventListener('click', metadataManagerScrapePlaceholder);
if ($('metadataManagerColumnPicker')) $('metadataManagerColumnPicker').addEventListener('change', e => {
  const target = e.target;
  if (target?.matches?.('input[data-column-key]')) metadataManagerSetColumnVisible(target.dataset.columnKey || '', !!target.checked);
});
if ($('metadataManagerColumnsReset')) $('metadataManagerColumnsReset').addEventListener('click', e => { e.preventDefault(); metadataManagerResetColumns(); });
if ($('metadataManagerColumnsShowAll')) $('metadataManagerColumnsShowAll').addEventListener('click', e => { e.preventDefault(); metadataManagerShowAllColumns(); });
if ($('metadataManagerTableBody')) $('metadataManagerTableBody').addEventListener('change', e => {
  const target = e.target;
  if (target?.classList?.contains('metadata-manager-row-check')) metadataManagerToggleSelection(target.dataset.id || '', target.checked);
});
if ($('metadataManagerTableBody')) $('metadataManagerTableBody').addEventListener('input', e => {
  const target = e.target;
  if (target?.classList?.contains('metadata-manager-input')) metadataManagerMarkDirty(target.dataset.id || '', target.dataset.field || '', target.value);
});
if ($('metadataManagerTableBody')) $('metadataManagerTableBody').addEventListener('pointerdown', metadataManagerHandlePreviewPointerDown);
document.addEventListener('pointerup', metadataManagerHandlePreviewPointerEnd);
document.addEventListener('pointercancel', metadataManagerHandlePreviewPointerEnd);
document.addEventListener('keydown', e => { if (e.key === 'Escape') metadataManagerHandlePreviewPointerEnd(); });
window.addEventListener('scroll', metadataManagerHandlePreviewPointerEnd, true);
if ($('settingsRescanLibrary')) $('settingsRescanLibrary').addEventListener('click', async e => { e.preventDefault(); await rescanLibrary(); });
if ($('settingsCleanupLibrary')) $('settingsCleanupLibrary').addEventListener('click', async e => { e.preventDefault(); await cleanupLibrary(); });
if ($('settingsEnrichMetadata')) $('settingsEnrichMetadata').addEventListener('click', async e => { e.preventDefault(); await enrichLibraryMetadata(); });
if ($('settingsAddLibrary')) $('settingsAddLibrary').addEventListener('click', e => { e.preventDefault(); openLibraryEditor(null); });
if ($('libraryEditorSave')) $('libraryEditorSave').addEventListener('click', e => { e.preventDefault(); saveLibraryEditor(); });
if ($('libraryEditorCancel')) $('libraryEditorCancel').addEventListener('click', e => { e.preventDefault(); closeLibraryEditor(); });
if ($('backToLibrary')) $('backToLibrary').addEventListener('click', () => showLibraryScreen());
if ($('backToLibraryFromDetails')) $('backToLibraryFromDetails').addEventListener('click', () => { showLibraryScreen(); render(); });
if ($('settingsSaveLibraryPath')) $('settingsSaveLibraryPath').addEventListener('click', async e => { e.preventDefault(); await saveLibraryPathFromInput('settingsLibraryPathInput','settingsLibraryStatus',null); });
setupRightPanelResize();
setupLibraryFolderBrowse();
syncRightToggleLabels();
loadReaderBackgrounds();
pollTasks(false);
installLibraryCardDelegates();
installGlobalDetailDelegate();
syncEmailTemplatePreview();
initializeGuidevaultAuthAndApp();

