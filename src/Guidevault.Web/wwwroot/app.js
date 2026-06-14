const state = {
  items: [], filtered: [], selected: null, filter: 'All Content', categoryFilter: '', viewMode: 'all', activeTab: 'overview', customFilter: null,
  reader: { item: null, pages: [], index: 0, animating: false, displayMode: 2, transitionMode: 'stable', overlayVisible: false, advancedVisible: false, bookmarkMenuOpen: false, magnifierSettingsVisible: false, scrubbing: false, shading: null, zoom: 100, fullscreenOnOpen: false, magnifier: null, magnifierActive: false, longPressTimer: null, suppressHitClickUntil: 0, backgrounds: [], background: '', backgroundBrightness: 72 },
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
  libraryIsPartial: false,
  librarySummary: null,
  libraryFullLoadPromise: null,
  deferredFullLibraryItems: null,
  libraryBackgroundLoad: { running: false, loaded: 0, total: 0 },
  virtualGrid: null,
  auth: { profile: null, authenticated: false, editing: false, appStarted: false },
  readingProfiles: { presets: {}, defaultPresetId: 'default', groupAssignments: {}, entryAssignments: {} },
  opds: { connectionUrl: '', selectedKeyId: '', keys: [], editingUrl: false, revealUrl: false, creatingKey: false },
  devices: { emailDevices: [], clientDevices: [], generatedAt: null, addingEmail: false, editingEmailId: '', editingClientId: '', clientMenuId: '' },
  metadataManager: { selectedIds: [], dirty: {}, filterKind: '', kindFilters: ['Manual','Strategy Guide','Magazine'], statusFilter: '', search: '', missing: '', category: '', visibleColumns: [], useCustomColumns: false, sortKey: '', sortDirection: 'asc', draggedColumnKey: '', renderLimit: 250 },
  serverFiles: { selectedIds: [], kindFilters: ['Manual','Strategy Guide','Magazine'], search: '', renderLimit: 250 },
  metadataSourceBatch: { results: [], running: false, applied: 0, runId: 0, abortController: null },
  keybinds: { bindings: {}, awaitingId: '' },
  folderBrowser: { targetInputId: '', currentPath: '/app/data/library', roots: [] },
  customize: { activeTab: 'home', homeShelves: [], sideNav: { customItems: [] } },
  serverSettings: null,
  emailSettings: null,
  emailHistory: [],
  systemEvents: [],
  usersSettings: { users: [], libraries: [], permissions: [] },
  usersSettingsRuntime: { loaded: false, loading: false, requestId: 0, loadTimer: 0, renderedHash: '' },
  taskSettings: null,
  homeShelfOffsets: {},
  statistics: { activeTab: 'stats', range: 'all' },
  profilePage: { activeTab: 'overview', range: 'all' },
  itemReviews: { cache: {}, loading: {} },
  preferences: { useColorscape: false, colorscapeDetailPane: true, colorscapeManualMenus: false, colorscapeStrategyMenus: false, colorscapeMagazineMenus: false },
  colorscape: { itemId: '', token: 0, menuToken: 0, cache: {}, persistentLoaded: false, cacheSaveTimer: 0, imageSampleQueue: [], imageSampleQueued: false },
  coverResults: { cache: {}, loaded: false, saveTimer: 0, serverPrewarmToken: 0, browserPrewarmQueued: false },
  systemInfo: null,
  performanceInfo: null,
  updateCheck: null,
  updateCheckTimer: null,
  updateToastTimer: null,
  settingsNavCollapsed: {},
  deviceHeartbeatTimer: null,
  openLibrary: { results: [], selectedResult: null, resolvedResult: null, step: 'search' },
  igdb: { results: [], selectedResult: null, resolvedResult: null, step: 'search' },
  esrb: { results: [], selectedResult: null, resolvedResult: null, step: 'search' }
};
const $ = id => document.getElementById(id);
function setText(id, value) {
  const el = $(id);
  if (el) el.textContent = value || '\u2014';
}
const READER_BOOKMARKS_KEY = 'guidevault.readerBookmarks.v1';
const READER_SHADING_KEY = 'guidevault.readerShading.v1';
const READER_MAGNIFIER_KEY = 'guidevault.readerMagnifier.v1';
const READER_BACKGROUND_KEY = 'guidevault.readerBackground.v1';
const READER_BACKGROUND_BRIGHTNESS_KEY = 'guidevault.readerBackgroundBrightness.v1';
const GUIDEVAULT_METADATA_OVERRIDES_KEY = 'guidevault.metadataOverrides.v2';
const GUIDEVAULT_METADATA_COLUMNS_KEY = 'guidevault.metadataManagerColumns.v1';
const GUIDEVAULT_METADATA_COLUMN_SORT_KEY = 'guidevault.metadataManagerColumnSort.v1';
const METADATA_MANAGER_DEFAULT_RENDER_LIMIT = 250;
const METADATA_MANAGER_RENDER_STEP = 250;
const METADATA_MANAGER_SEARCH_DEBOUNCE_MS = 220;
const GUIDEVAULT_LOGIN_PROFILE_KEY = 'guidevault.localLoginProfile.v1';
const GUIDEVAULT_READING_PROFILES_KEY = 'guidevault.readingProfiles.v1';
const GUIDEVAULT_OPDS_SETTINGS_KEY = 'guidevault.opdsSettings.v1';
const GUIDEVAULT_PREFERENCES_KEY = 'guidevault.preferences.v1';
const GUIDEVAULT_SETTINGS_NAV_KEY = 'guidevault.settingsNavCollapsed.v1';
const GUIDEVAULT_KEYBINDS_KEY = 'guidevault.keybinds.v1';
const GUIDEVAULT_CUSTOMIZE_KEY = 'guidevault.customize.v1';
let guidevaultCustomizeSaveTimer = null;
let guidevaultCustomizeSyncInFlight = false;
let guidevaultStartupDeepLinkHandled = false;
const GUIDEVAULT_READING_ACTIVITY_KEY = 'guidevault.readingActivity.v1';
const GUIDEVAULT_READING_ACTIVITY_LIMIT = 1000;
const GUIDEVAULT_PROFILE_ACTIVITY_DISPLAY_LIMIT = 100;
const GUIDEVAULT_PROFILE_REVIEWS_KEY = 'guidevault.profileReviews.v1';
const GUIDEVAULT_CATEGORY_STRUCTURE_KEY = 'guidevault.categoryStructure.v1';
const GUIDEVAULT_COVER_SIZE_KEY = 'guidevault.libraryCoverSize.v1';
const GUIDEVAULT_FAVORITES_KEY = 'guidevault.favorites.v1';
const GUIDEVAULT_LIBRARY_CACHE_KEY = 'guidevault.libraryCache.v1';
const GUIDEVAULT_GRID_INITIAL_RENDER = 96;
const GUIDEVAULT_GRID_CHUNK_SIZE = 96;
const GUIDEVAULT_GRID_VIRTUAL_THRESHOLD = 5000;
const GUIDEVAULT_GRID_VIRTUAL_BUFFER_ROWS = 5;
const GUIDEVAULT_GRID_VIRTUAL_MAX_CARDS = 180;
const GUIDEVAULT_LIBRARY_STARTUP_CACHE_LIMIT = 240;
const GUIDEVAULT_LIBRARY_CACHE_MAX_BYTES = 1500000;
const GUIDEVAULT_LIBRARY_FULL_RENDER_DELAY_MS = 900;
const GUIDEVAULT_LIBRARY_CHUNK_SIZE = 220;
const GUIDEVAULT_LIBRARY_CHUNK_YIELD_MS = 30;
const GUIDEVAULT_STARTUP_STATUS_HIDE_MS = 2400;
const GUIDEVAULT_LIBRARY_SEARCH_DEBOUNCE_MS = 180;
const GUIDEVAULT_SORT_COLLATOR = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });
const GUIDEVAULT_APP_VERSION = '0.9.197';
const GUIDEVAULT_FILENAME_SCHEMA_KEY = 'guidevault.filenameRename.schema.v1';
const GUIDEVAULT_FILE_ORGANIZATION_TEMPLATE_PRESETS_KEY = 'guidevault.fileOrganization.templatePresets.v2';
const GUIDEVAULT_FILE_ORGANIZATION_TEMPLATE_PRESETS_LEGACY_KEY = 'guidevault.fileOrganization.templatePresets.v1';
const SERVER_FILES_PREVIEW_RENDER_LIMIT = 175;
const GUIDEVAULT_DEFAULT_FILENAME_SCHEMA = '{title}';
const GUIDEVAULT_STABLE_TAG_FEED_URL = 'https://api.github.com/repos/Shredder5262/GuideVault/tags';
const GUIDEVAULT_RELEASES_URL = 'https://github.com/Shredder5262/GuideVault/releases';
const GUIDEVAULT_PACKAGE_URL = 'https://github.com/Shredder5262/GuideVault/pkgs/container/guidevault';
const GUIDEVAULT_CURRENT_IMAGE = 'ghcr.io/shredder5262/guidevault:latest';
const GUIDEVAULT_COLORSCAPE_CACHE_KEY = 'guidevault.colorscapeCache.v1';
const GUIDEVAULT_COLORSCAPE_CACHE_LIMIT = 900;
const GUIDEVAULT_COLORSCAPE_MENU_BATCH_SIZE = 2;
const GUIDEVAULT_COLORSCAPE_MENU_SAMPLE_DELAY_MS = 700;
const GUIDEVAULT_SECONDARY_COVER_DELAY_MS = 3200;
const GUIDEVAULT_SECONDARY_COVER_BATCH_SIZE = 2;
const GUIDEVAULT_PRIMARY_COVER_BATCH_SIZE = 5;
const GUIDEVAULT_PRIMARY_COVER_DELAY_MS = 180;
const GUIDEVAULT_COVER_VIEWPORT_PRIME_PADDING = 520;
const GUIDEVAULT_CATEGORY_PRIMARY_PREWARM_LIMIT = 96;
const GUIDEVAULT_CATEGORY_PRIMARY_PREWARM_BATCH_SIZE = 8;
const GUIDEVAULT_CATEGORY_PRIMARY_PREWARM_DELAY_MS = 110;
const GUIDEVAULT_CATEGORY_PREVIEW_PREWARM_LIMIT = 220;
const GUIDEVAULT_CATEGORY_PREVIEW_PREWARM_BATCH_SIZE = 16;
const GUIDEVAULT_CATEGORY_PREVIEW_PREWARM_DELAY_MS = 42;
const GUIDEVAULT_CATEGORY_VISIBLE_COVER_EAGER_LIMIT = 16;
const GUIDEVAULT_COVER_RESULT_CACHE_KEY = 'guidevault.coverResultCache.v1';
const GUIDEVAULT_COVER_OVERRIDE_BUST_KEY = 'guidevault.coverOverrideBust.v1';
const GUIDEVAULT_COVER_RESULT_CACHE_LIMIT = 1600;
const GUIDEVAULT_TRANSPARENT_COVER_PLACEHOLDER = 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==';
const GUIDEVAULT_GRID_COVER_THUMB_WIDTH = 360;

let guidevaultLibrarySearchTimer = null;
let guidevaultVirtualResizeAttached = false;
let guidevaultLibrarySearchCache = typeof WeakMap === 'function' ? new WeakMap() : null;
let guidevaultCategoryRenderKey = '';
let guidevaultCategoryRenderTimer = 0;
let guidevaultGroupGridCacheVersion = -1;
let guidevaultGroupGridCache = new Map();
let guidevaultHomeShelfCacheKey = '';
let guidevaultHomeShelfCache = new Map();
let guidevaultStartupStatusHideTimer = null;
let guidevaultSecondaryCoverPrimeTimer = 0;
const guidevaultSecondaryCoverPrimeQueue = new Set();
let guidevaultPrimaryCoverPrimeTimer = 0;
const guidevaultPrimaryCoverPrimeQueue = new Set();
let guidevaultCategoryPrimaryPrewarmTimer = 0;
const guidevaultCategoryPrimaryPrewarmQueue = [];
const guidevaultCategoryPrimaryPrewarmSeen = new Set();
let guidevaultCategoryPreviewPrewarmTimer = 0;
const guidevaultCategoryPreviewPrewarmQueue = [];
const guidevaultCategoryPreviewPrewarmSeen = new Set();

const METADATA_STATUS_OPTIONS = ['Unreviewed', 'Needs Review', 'Reviewed', 'Locked', 'Failed Lookup', 'Manual Only'];
const METADATA_STATUS_ALIASES = new Map([
  ['unreviewed', 'Unreviewed'], ['unscanned', 'Unreviewed'], ['new', 'Unreviewed'],
  ['needsreview', 'Needs Review'], ['needs review', 'Needs Review'], ['needs-review', 'Needs Review'], ['review', 'Needs Review'], ['partial', 'Needs Review'], ['partial match', 'Needs Review'],
  ['reviewed', 'Reviewed'], ['verified', 'Reviewed'], ['complete', 'Reviewed'],
  ['locked', 'Locked'], ['protected', 'Locked'],
  ['failedlookup', 'Failed Lookup'], ['failed lookup', 'Failed Lookup'], ['lookup failed', 'Failed Lookup'], ['no match', 'Failed Lookup'], ['no-match', 'Failed Lookup'],
  ['manualonly', 'Manual Only'], ['manual only', 'Manual Only'], ['manual-only', 'Manual Only'], ['manual', 'Manual Only']
]);

function normalizeMetadataStatus(value, fallback = 'Unreviewed') {
  const text = String(value ?? '').trim();
  if (!text) return fallback;
  const lower = text.toLowerCase();
  const compact = lower.replace(/[^a-z0-9]+/g, '');
  return METADATA_STATUS_ALIASES.get(lower) || METADATA_STATUS_ALIASES.get(compact) || text;
}

function metadataStatusOf(item = {}) {
  return normalizeMetadataStatus(item?.metadataStatus || item?.MetadataStatus || item?.metadataReviewStatus || item?.reviewStatus || 'Unreviewed');
}

function metadataStatusOptionsHtml(selected = '') {
  const value = normalizeMetadataStatus(selected || 'Unreviewed');
  return METADATA_STATUS_OPTIONS.map(option => `<option value="${escapeForAttribute(option)}" ${option === value ? 'selected' : ''}>${escapeHtml(option)}</option>`).join('');
}

function metadataStatusPillHtml(status = 'Unreviewed') {
  const normalized = normalizeMetadataStatus(status || 'Unreviewed');
  const slug = normalized.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'unreviewed';
  return `<span class="metadata-status-pill metadata-status-${escapeForAttribute(slug)}">${escapeHtml(normalized)}</span>`;
}


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
const GUIDEVAULT_UPDATE_NOTIFIED_VERSION_KEY = 'guidevault.update.notifiedVersion.v1';
const fmtBytes = n => n > 1024 ** 3 ? `${(n / 1024 ** 3).toFixed(1)} GB` : `${(n / 1024 ** 2).toFixed(1)} MB`;
const MULTI_PLATFORM_LABEL = 'Multi-Platform';
const IGDB_PLATFORM_NAME_OVERRIDES = new Map([
  ['pc microsoft windows', 'Windows'],
  ['microsoft windows', 'Windows'],
  ['windows pc', 'Windows'],
  ['pc', 'Windows'],
  ['dos', 'MS-DOS'],
  ['ms dos', 'MS-DOS'],
  ['msdos', 'MS-DOS'],
  ['pc dos', 'MS-DOS'],
  ['ibm pc dos', 'MS-DOS'],
  ['playstation 5', 'Sony Playstation 5'],
  ['sony playstation 5', 'Sony Playstation 5'],
  ['ps5', 'Sony Playstation 5'],
  ['playstation 4', 'Sony Playstation 4'],
  ['sony playstation 4', 'Sony Playstation 4'],
  ['ps4', 'Sony Playstation 4'],
  ['playstation 3', 'Sony Playstation 3'],
  ['sony playstation 3', 'Sony Playstation 3'],
  ['ps3', 'Sony Playstation 3'],
  ['playstation 2', 'Sony Playstation 2'],
  ['sony playstation 2', 'Sony Playstation 2'],
  ['ps2', 'Sony Playstation 2'],
  ['playstation', 'Sony Playstation'],
  ['sony playstation', 'Sony Playstation'],
  ['ps1', 'Sony Playstation'],
  ['psx', 'Sony Playstation'],
  ['playstation portable', 'Sony PSP'],
  ['sony playstation portable', 'Sony PSP'],
  ['psp', 'Sony PSP'],
  ['dreamcast', 'Sega Dreamcast'],
  ['sega dreamcast', 'Sega Dreamcast'],
  ['sega dreamcase', 'Sega Dreamcast'],
  ['xbox', 'Microsoft Xbox'],
  ['microsoft xbox', 'Microsoft Xbox']
]);
function platformNormalizationKey(value = '') {
  return String(value || '')
    .toLowerCase()
    .replace(/[()]/g, ' ')
    .replace(/[^a-z0-9]+/g, ' ')
    .trim()
    .replace(/\s+/g, ' ');
}
function normalizeGuidevaultPlatformName(value = '') {
  const raw = String(value || '').trim();
  if (!raw) return '';
  if (isMultiPlatformBucketName(raw)) return MULTI_PLATFORM_LABEL;
  return IGDB_PLATFORM_NAME_OVERRIDES.get(platformNormalizationKey(raw)) || raw;
}
function normalizeGuidevaultPlatformList(value) {
  const parts = Array.isArray(value)
    ? value
    : String(value || '').split(/[;,|]/);
  const normalized = [];
  parts.forEach(platform => pushUniquePlatformBucket(normalized, normalizeGuidevaultPlatformName(platform)));
  return normalized;
}
const rawCategoryOf = item => {
  if (item?.kind === 'Magazine') return String(item.magazineTitle || item.series || 'Unsorted Magazines').trim() || 'Unsorted Magazines';
  return normalizeGuidevaultPlatformName(item?.category || item?.system || 'Unsorted') || 'Unsorted';
};
const categoryOf = item => item?._gvComputed?.category || rawCategoryOf(item);
const associatedPlatformsOf = item => item?._gvComputed?.associatedPlatforms || normalizeGuidevaultPlatformList(item?.associatedPlatforms || []);
const platformListText = item => item?._gvComputed?.platformListText || associatedPlatformsOf(item).join(', ');
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
  if (item?.kind === 'Magazine') return '';
  if (item?.kind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(item)) return MULTI_PLATFORM_LABEL;
  return item?.category || item?.system || item?.primarySystem || '';
}
function activeLibraryPlatformForItem(item) {
  if (!item || !state.categoryFilter) return '';
  const parts = String(state.categoryFilter).split('::');
  const kind = parts.shift() || '';
  const category = parts.join('::');
  if (!category) return '';
  const mode = sidebarCategoryModeConfig(kind);
  if (mode) {
    if (mode.kind && item.kind !== mode.kind) return '';
    return sidebarCategoryValuesForItem(item, kind).some(value => platformNamesEqual(value, category)) ? category : '';
  }
  if (kind && item.kind !== kind) return '';
  return libraryCategoryKeysForItem(item).some(value => platformNamesEqual(value, category)) ? category : '';
}
function detailSystemLabelForItem(item) {
  if (!item) return '\u2014';
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
    'Sony Playstation': 'PS',
    'Sony PlayStation': 'PS',
    'PlayStation 2': 'PS2',
    'Sony Playstation 2': 'PS2',
    'Sony PlayStation 2': 'PS2',
    'PlayStation 3': 'PS3',
    'Sony Playstation 3': 'PS3',
    'Sony PlayStation 3': 'PS3',
    'PlayStation 4': 'PS4',
    'Sony Playstation 4': 'PS4',
    'Sony PlayStation 4': 'PS4',
    'PlayStation 5': 'PS5',
    'Sony Playstation 5': 'PS5',
    'Sony PlayStation 5': 'PS5',
    'PlayStation Portable': 'PSP',
    'Sony PSP': 'PSP',
    'Sony Playstation Vita': 'VITA',
    'Sony PlayStation Vita': 'VITA',
    'Sega Genesis': 'GEN',
    'Sega Dreamcast': 'DC',
    'MS-DOS': 'DOS',
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
function magazinePublicationIconHtml(sizeClass = 'magazine-publication-icon') {
  return `<span class="${escapeForAttribute(sizeClass)}" aria-hidden="true">\u25A6</span>`;
}
function categoryDisplayIconHtml(kind, name, sizeClass = 'platform-icon tiny') {
  return kind === 'Magazine' ? magazinePublicationIconHtml(sizeClass.includes('large') ? 'magazine-publication-icon large' : 'magazine-publication-icon tiny') : platformIconHtml(name, sizeClass);
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
  const icon = categoryDisplayIconHtml(item?.kind || '', category, 'platform-icon tiny');
  return `<small class="card-category">${icon}<span>${escapeHtml(category)}${hasSequence(item) ? ` \u2022 #${escapeHtml(item.issueNumber)}` : ''}</span></small>`;
}
function isMultiPlatformBucketName(value) {
  return /^multi[-\s]*platform(?: strategy guides?)?$/i.test(String(value || '').trim());
}
function pushUniquePlatformBucket(values, value) {
  const text = String(value || '').trim();
  if (!text || text === '\u2014' || /^unknown$/i.test(text) || isMultiPlatformBucketName(text)) return;
  if (!values.some(existing => existing.localeCompare(text, undefined, { sensitivity: 'accent' }) === 0)) values.push(text);
}
function libraryCategoryKeysForItem(item) {
  const values = [];
  if (!item) return ['Unsorted'];
  if (item.kind === 'Magazine') {
    pushUniquePlatformBucket(values, item.magazineTitle || item.series || '');
    return values.length ? values : ['Unsorted Magazines'];
  }
  const preferred = item.category || item.system || item.primarySystem || '';
  if (item.kind === 'Strategy Guide') {
    associatedPlatformsOf(item).forEach(platform => pushUniquePlatformBucket(values, platform));
    if (!values.length) pushUniquePlatformBucket(values, preferred);
    return values.length ? values : ['Unsorted Strategy Guides'];
  }
  pushUniquePlatformBucket(values, preferred);
  return values.length ? values : ['Unsorted'];
}
function itemMatchesCategoryFilter(item, categoryFilter = state.categoryFilter) {
  if (!categoryFilter) return true;
  const parts = String(categoryFilter).split('::');
  const filterKind = parts.shift() || '';
  const category = parts.join('::');
  if (!category) return true;
  const mode = sidebarCategoryModeConfig(filterKind);
  if (mode?.valueForItem) {
    if (mode.kind && item?.kind !== mode.kind) return false;
    return sidebarCategoryValuesForItem(item, filterKind).some(value => value.localeCompare(category, undefined, { sensitivity: 'accent' }) === 0);
  }
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
const rawDisplayTitle = item => {
  if (!item) return '';
  const title = String(item.title || item.name || '').trim();
  if (title) return title;
  if (item.kind === 'Magazine') {
    const publication = String(item.magazineTitle || item.series || '').trim();
    const issue = String(item.issueNumber || '').trim();
    return publication && issue ? `${publication} #${issue}` : (publication || 'Untitled Magazine Issue');
  }
  return item.title || '';
};
const displayTitle = item => item?._gvComputed?.title || rawDisplayTitle(item);
const rawHasSequence = item => item?.kind === 'Magazine' && !!String(item.issueNumber || '').trim();
const hasSequence = item => item?._gvComputed?.hasIssue ?? rawHasSequence(item);
const rawIssueValue = item => Number.parseFloat(String(item?.issueNumber || '').replace(/[^0-9.]/g, '')) || 0;
const issueValue = item => item?._gvComputed?.issue ?? rawIssueValue(item);

function libraryItemComputed(item) {
  if (!item || typeof item !== 'object') return {};
  if (item._gvComputed) return item._gvComputed;
  const title = rawDisplayTitle(item);
  const category = rawCategoryOf(item);
  const associatedPlatforms = normalizeGuidevaultPlatformList(item?.associatedPlatforms || []);
  const computed = {
    title,
    category,
    associatedPlatforms,
    platformListText: associatedPlatforms.join(', '),
    hasIssue: rawHasSequence(item),
    issue: rawIssueValue(item),
    recent: itemRecentTimestamp(item),
    alpha: alphaKey(title),
    kind: String(item.kind || '')
  };
  try {
    Object.defineProperty(item, '_gvComputed', { value: computed, enumerable: false, configurable: true });
  } catch {
    item._gvComputed = computed;
  }
  return computed;
}

function prepareLibraryItemComputedFields(item) {
  if (!item || typeof item !== 'object') return item;
  try { delete item._gvComputed; } catch {}
  libraryItemComputed(item);
  return item;
}

function prepareLibraryItemsForClient(items = state.items) {
  (Array.isArray(items) ? items : []).forEach(prepareLibraryItemComputedFields);
  return items;
}

function markLibraryIndexesDirty() {
  state.libraryCategoryCacheVersion = (Number(state.libraryCategoryCacheVersion || 0) + 1) % 1000000;
  guidevaultCategoryRenderKey = '';
  guidevaultGroupGridCacheVersion = -1;
  guidevaultGroupGridCache = new Map();
  guidevaultHomeShelfCacheKey = '';
  guidevaultHomeShelfCache = new Map();
  const categoriesHost = $('categories');
  if (categoriesHost) categoriesHost.dataset.categoryCacheKey = '';
}

function refreshLibraryDerivedState() {
  prepareLibraryItemsForClient(state.items);
  state._countCache = null;
  clearLibrarySearchCaches();
  markLibraryIndexesDirty();
}


function defaultReadingProfile() {
  return {
    displayMode: 2,
    transitionMode: 'stable',
    background: '',
    backgroundBrightness: 72,
    zoom: 100,
    fullscreenOnOpen: false
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
    backgroundBrightness: state.reader.backgroundBrightness ?? loadReaderBackgroundBrightness(),
    zoom: state.reader.zoom,
    fullscreenOnOpen: !!state.reader.fullscreenOnOpen
  });
}

function normalizeReaderTransitionMode(mode) {
  const value = String(mode || '');
  if (value === 'dissolve') return 'fade';
  if (value === 'push') return 'slide';
  const allowed = new Set(['stable', 'fade', 'slide', 'page']);
  return allowed.has(value) ? value : 'stable';
}

function normalizeReadingProfile(value = {}) {
  const defaults = defaultReadingProfile();
  return {
    displayMode: normalizeReaderDisplayMode(value.displayMode ?? value.pageTypeDisplay ?? defaults.displayMode),
    transitionMode: normalizeReaderTransitionMode(value.transitionMode ?? value.transitionType ?? defaults.transitionMode),
    background: String((value.background ?? value.backgroundType ?? defaults.background) || '').trim(),
    backgroundBrightness: clampNumber(value.backgroundBrightness ?? value.brightness ?? value.readerBrightness ?? defaults.backgroundBrightness, 15, 100, defaults.backgroundBrightness),
    zoom: clampNumber(value.zoom ?? value.zoomLevel ?? defaults.zoom, 70, 145, defaults.zoom),
    fullscreenOnOpen: !!(value.fullscreenOnOpen ?? value.openFullscreen ?? value.defaultFullscreen ?? defaults.fullscreenOnOpen),
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
    backgroundBrightness: base.backgroundBrightness,
    zoom: base.zoom,
    fullscreenOnOpen: base.fullscreenOnOpen,
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
  const fullscreen = normalized.fullscreenOnOpen ? ' \u2022 fullscreen on open' : '';
  return `${displayModeLabel(normalized.displayMode)} \u2022 ${transitionLabel(normalized.transitionMode)} \u2022 ${background} \u2022 ${normalized.backgroundBrightness}% brightness \u2022 ${normalized.zoom}% zoom${fullscreen}`;
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
    const defaultBadge = preset.id === profiles.defaultPresetId ? ' \u2014 Default' : '';
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
    brightness: 'readingProfilePresetBrightness',
    brightnessValue: 'readingProfilePresetBrightnessValue',
    zoom: 'readingProfilePresetZoom',
    zoomValue: 'readingProfilePresetZoomValue',
    fullscreen: 'readingProfilePresetFullscreen',
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
  if ($(ids.brightness)) $(ids.brightness).value = String(normalized.backgroundBrightness);
  if ($(ids.brightnessValue)) $(ids.brightnessValue).textContent = `${normalized.backgroundBrightness}%`;
  if ($(ids.zoom)) $(ids.zoom).value = String(normalized.zoom);
  if ($(ids.zoomValue)) $(ids.zoomValue).textContent = `${normalized.zoom}%`;
  if ($(ids.fullscreen)) $(ids.fullscreen).checked = !!normalized.fullscreenOnOpen;
  if ($('readingProfileDeletePreset')) $('readingProfileDeletePreset').disabled = normalized.id === 'default';
}

function getReadingProfilePresetFormValues() {
  const ids = readingProfilePresetFormIds();
  return normalizeReadingProfile({
    displayMode: $(ids.display)?.value,
    transitionMode: $(ids.transition)?.value,
    background: $(ids.background)?.value,
    backgroundBrightness: $(ids.brightness)?.value,
    zoom: $(ids.zoom)?.value,
    fullscreenOnOpen: !!$(ids.fullscreen)?.checked
  });
}

function refreshReadingProfilePresetZoomOutput() {
  const ids = readingProfilePresetFormIds();
  const zoom = clampNumber($(ids.zoom)?.value, 70, 145, 100);
  if ($(ids.zoomValue)) $(ids.zoomValue).textContent = `${zoom}%`;
}

function refreshReadingProfilePresetBrightnessOutput() {
  const ids = readingProfilePresetFormIds();
  const brightness = clampNumber($(ids.brightness)?.value, 15, 100, 72);
  if ($(ids.brightnessValue)) $(ids.brightnessValue).textContent = `${brightness}%`;
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
  const selectedId = $('readingProfilePresetSelect')?.value || profiles.defaultPresetId || 'default';
  if (!presets.length) {
    list.innerHTML = '<p class="sub">No reading profile presets yet.</p>';
    return;
  }
  list.innerHTML = presets.map(preset => {
    const badges = [];
    if (preset.id === profiles.defaultPresetId) badges.push('<span class="pill reading-profile-default-pill">Default</span>');
    if (preset.id === selectedId) badges.push('<span class="pill reading-profile-active-pill">Selected</span>');
    const displayLabel = displayModeLabel(preset.displayMode || preset.display || 1);
    const transitionText = transitionLabel(preset.transitionMode || preset.transition || 'stable');
    return `<button class="reading-profile-preset-row${preset.id === selectedId ? ' active' : ''}" type="button" data-profile-id="${escapeHtml(preset.id)}">
      <span class="reading-profile-preset-main"><b>${escapeHtml(preset.name || 'Reading Profile')}</b><em>${escapeHtml(readingProfileLabel(preset))}</em></span>
      <span class="reading-profile-preset-visual"><span>${escapeHtml(displayLabel)}</span><span>${escapeHtml(transitionText)}</span></span>
      <span class="reading-profile-preset-badges">${badges.join('')}</span>
    </button>`;
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
      : `Inherited from ${group?.type === 'series' ? 'series' : 'category'} \u201C${group?.label || 'Unsorted'}\u201D.`;
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
  state.reader.backgroundBrightness = profile.backgroundBrightness;
  state.reader.zoom = profile.zoom;
  state.reader.fullscreenOnOpen = !!profile.fullscreenOnOpen;
}

async function requestReaderFullscreenFromProfile() {
  if (!state.reader.fullscreenOnOpen || document.fullscreenElement) return;
  const el = $('readerStage') || $('readerView');
  if (!el?.requestFullscreen) return;
  try {
    await el.requestFullscreen();
  } catch (err) {
    console.info('Reading profile fullscreen-on-open could not start automatically. The browser may require a fresh user gesture.', err);
  }
  updateReaderFullscreenUi();
  window.setTimeout(refreshReaderBookSize, 80);
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
    createdAt: value.createdAt || '',
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
    identity: $(`${prefix}Identity`)?.value?.trim() || '',
    password: $(`${prefix}Password`)?.value || '',
    avatarDataUrl: state.auth.profile?.avatarDataUrl || readLoginProfile()?.avatarDataUrl || ''
  };
}
function normalizeLoginIdentity(value = '') {
  return String(value || '').trim().toLowerCase();
}
function validateLoginProfile(profile) {
  if (!profile.username || !profile.email || !profile.password) return 'Username, email, and password are required.';
  if (!profile.email.includes('@') || profile.email.startsWith('@') || profile.email.endsWith('@')) return 'Enter a valid email address.';
  return '';
}
function validateExistingLoginCredentials(form) {
  if (!form.identity || !form.password) return 'Username / email address and password are required.';
  return '';
}
function profileMatchesLoginIdentity(profile, identity) {
  const value = normalizeLoginIdentity(identity);
  return !!value && [profile?.username, profile?.email]
    .map(normalizeLoginIdentity)
    .filter(Boolean)
    .includes(value);
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

  const identityMode = !firstUse;
  const identityLabel = $('loginIdentityLabel');
  const identityInput = $('loginIdentity');
  const usernameLabel = $('loginUsernameLabel');
  const usernameInput = $('loginUsername');
  const emailLabel = $('loginEmailLabel');
  const emailInput = $('loginEmail');

  identityLabel?.classList.toggle('hidden', !identityMode);
  identityInput?.classList.toggle('hidden', !identityMode);
  usernameLabel?.classList.toggle('hidden', identityMode);
  usernameInput?.classList.toggle('hidden', identityMode);
  emailLabel?.classList.toggle('hidden', identityMode);
  emailInput?.classList.toggle('hidden', identityMode);

  if (identityInput) {
    identityInput.disabled = !identityMode;
    identityInput.required = identityMode;
    identityInput.value = identityMode ? (profile.username || profile.email || '') : '';
  }
  if (usernameInput) {
    usernameInput.disabled = identityMode;
    usernameInput.required = !identityMode;
    usernameInput.value = identityMode ? '' : (usernameInput.value || '');
  }
  if (emailInput) {
    emailInput.disabled = identityMode;
    emailInput.required = !identityMode;
    emailInput.value = identityMode ? '' : (emailInput.value || '');
  }

  if ($('loginAction')) $('loginAction').textContent = firstUse ? 'Create Local Login' : 'Sign In';
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
  requestAnimationFrame(() => ($('loginIdentity') && !$('loginIdentity').classList.contains('hidden') ? $('loginIdentity') : $('loginUsername'))?.focus?.());
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
  const avatarUrl = avatar ? `url("${avatar.replace(/"/g, '%22')}")` : '';
  el.textContent = avatar ? '' : userInitials(profile);
  el.classList.toggle('has-image', !!avatar);
  if (avatarUrl) {
    el.style.setProperty('--guidevault-avatar-image', avatarUrl);
    el.style.backgroundImage = avatarUrl;
  } else {
    el.style.removeProperty('--guidevault-avatar-image');
    el.style.removeProperty('background-image');
  }
}
function profileAvatarCssUrl(profile = state.auth.profile || readLoginProfile() || {}) {
  const avatar = String(profile.avatarDataUrl || '').trim();
  return avatar ? `url("${avatar.replace(/"/g, '%22')}")` : '';
}
function renderProfileHeroBackground(profile = state.auth.profile || readLoginProfile() || {}) {
  const hero = $('profileHero');
  if (!hero) return;
  const image = profileAvatarCssUrl(profile);
  hero.classList.toggle('has-profile-hero-image', !!image);
  if (image) hero.style.setProperty('--profile-hero-image', image);
  else hero.style.removeProperty('--profile-hero-image');
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
  if ($('accountProfileSummary')) $('accountProfileSummary').textContent = `${profile.username} \u2022 ${profile.email}`;
  renderUserAvatarElement($('accountAvatarPreview'), profile);
  syncTopUserMenu();
  setAccountEditMode(false, false);
}

function formatProfileRelativeTime(value) {
  const time = dateValue(value);
  if (!time) return '\u2014';
  const diff = Date.now() - time;
  if (diff < 86400000) return 'today';
  const days = Math.max(1, Math.floor(diff / 86400000));
  if (days < 31) return `${days} day${days === 1 ? '' : 's'} ago`;
  const months = Math.floor(days / 30);
  if (months < 24) return `${months} month${months === 1 ? '' : 's'} ago`;
  const years = Math.floor(days / 365);
  return `${years} year${years === 1 ? '' : 's'} ago`;
}
function profileUserKey(profile = state.auth.profile || readLoginProfile() || {}) {
  return String(profile.username || profile.email || 'local user').trim().toLowerCase() || 'local user';
}
function profileEventsForRange(rangeOverride = null) {
  const range = rangeOverride ?? (state.profilePage?.range || 'all');
  const profile = state.auth.profile || readLoginProfile() || {};
  const userKey = profileUserKey(profile);
  const cutoff = range === 'month' ? Date.now() - 31 * 86400000 : range === 'year' ? new Date(new Date().getFullYear(), 0, 1).getTime() : 0;
  return readReadingActivity()
    .filter(e => {
      const eventUser = String(e.user || userKey).toLowerCase();
      const at = dateValue(e.at);
      return eventUser === userKey && (!cutoff || at >= cutoff);
    })
    .sort((a, b) => (dateValue(a.at) || 0) - (dateValue(b.at) || 0));
}
function profileItemLookup() {
  return new Map((state.items || []).map(item => [String(item.id || item.Id || ''), item]));
}
function isProfileReadEvent(event) {
  const action = String(event?.action || '').trim().toLowerCase();
  return ['read', 'open-reader', 'reader', 'pdf'].includes(action);
}
function profileReadingEvents(events = []) {
  const readOnly = (events || []).filter(isProfileReadEvent);
  return readOnly.length ? readOnly : (events || []);
}
function eventItemPageCount(event = {}, item = null) {
  return Number(event.pageCount || event.pages || item?.pageCount || item?.pages || item?.PageCount || 0) || 0;
}
function estimatedMinutesForReadEvent(event = {}, lookup = new Map()) {
  const explicit = Number(event.minutes || event.durationMinutes || event.readMinutes || event.readTimeMinutes || 0);
  if (Number.isFinite(explicit) && explicit > 0) return explicit;
  const item = lookup.get(String(event.id || '')) || null;
  const pages = eventItemPageCount(event, item);
  if (pages > 0) return Math.max(3, Math.round(pages * 0.75));
  return 5;
}
function formatProfileDate(value) {
  const time = dateValue(value);
  if (!time) return '\u2014';
  return new Date(time).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}
function formatProfileDateWithRelative(value) {
  const time = dateValue(value);
  if (!time) return '\u2014';
  return `${formatProfileDate(time)} (${formatProfileRelativeTime(time)})`;
}
function profilePageStats() {
  const profile = state.auth.profile || readLoginProfile() || {};
  const events = profileReadingEvents(profileEventsForRange());
  const allEventsRaw = profileEventsForRange('all');
  const allEvents = profileReadingEvents(allEventsRaw);
  const lookup = profileItemLookup();
  const uniqueIds = [...new Set(events.map(e => String(e.id || '')).filter(Boolean))];
  const uniqueItems = uniqueIds.map(id => lookup.get(id)).filter(Boolean);
  const fallbackItems = uniqueItems.length ? uniqueItems : events.map(e => ({ kind: e.kind, title: e.title, pageCount: e.pageCount || 0 }));
  const pages = fallbackItems.reduce((sum, item) => sum + (Number(item.pageCount || item.pages || item.PageCount || 0) || 0), 0);
  const estimatedMinutes = events.reduce((sum, event) => sum + estimatedMinutesForReadEvent(event, lookup), 0);
  const allEstimatedMinutes = allEvents.reduce((sum, event) => sum + estimatedMinutesForReadEvent(event, lookup), 0);
  const firstActivityAt = allEventsRaw.reduce((min, e) => {
    const at = dateValue(e.at);
    return at && (!min || at < min) ? at : min;
  }, 0);
  const joined = dateValue(profile.createdAt) || firstActivityAt || 0;
  const weeks = joined ? Math.max(1, Math.ceil((Date.now() - joined) / (7 * 86400000))) : Math.max(1, Math.ceil((Date.now() - (firstActivityAt || Date.now())) / (7 * 86400000)));
  const lastRead = allEvents.reduce((max, e) => Math.max(max, dateValue(e.at) || 0), 0);
  return {
    profile,
    events,
    allEvents,
    allEventsRaw,
    lookup,
    uniqueIds,
    uniqueItems,
    totalReads: events.length,
    allReadCount: allEvents.length,
    manuals: events.filter(e => normalizeReadingKindGroup(e.kind) === 'Manual').length,
    strategyGuides: events.filter(e => normalizeReadingKindGroup(e.kind) === 'Strategy Guide').length,
    magazines: events.filter(e => normalizeReadingKindGroup(e.kind) === 'Magazine').length,
    pages,
    words: Math.round(pages * 420),
    authors: new Set(uniqueItems.map(i => i.writer || i.author || i.publisher).filter(Boolean)).size,
    ratings: Object.keys(state.favorites || {}).length,
    estimatedMinutes,
    allEstimatedMinutes,
    avgPerWeek: allEstimatedMinutes / weeks,
    joined,
    lastRead
  };
}
function formatProfileMinutes(minutes) {
  const n = Math.max(0, Math.round(Number(minutes || 0)));
  if (n >= 60) return `${(n / 60).toFixed(1)} hours`;
  return `${n} minutes`;
}
function profileMetricIcon(key) {
  const icons = {
    manuals: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4.5 5.5h6A3.5 3.5 0 0 1 14 9v10.5a3.5 3.5 0 0 0-3.5-3.5h-6V5.5Z"/><path d="M14 9a3.5 3.5 0 0 1 3.5-3.5h2v10.5h-2A3.5 3.5 0 0 0 14 19.5"/></svg>',
    strategy: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 18V6l8-3 8 3v12l-8 3-8-3Z"/><path d="M12 3v18M4 6l8 3 8-3M8 12h8M8 16h5"/></svg>',
    magazines: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 4h10a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3V5a1 1 0 0 1 1-1Z"/><path d="M8 8h7M8 11h7M8 14h4"/></svg>',
    pages: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M7 3h8l4 4v14H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z"/><path d="M15 3v5h5M8 12h8M8 16h8"/></svg>',
    words: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 6h16M4 10h16M4 14h11M4 18h8"/><path d="M17 14l1.4 4 1.6-4 1.5 4 1.5-4"/></svg>',
    publishers: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 20V8l7-4 7 4v12"/><path d="M8 20v-7h8v7M9 9h.01M12 9h.01M15 9h.01"/></svg>',
    favorites: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m12 3 2.7 5.5 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.8 1-6.1-4.4-4.3 6.1-.9L12 3Z"/></svg>'
  };
  return icons[key] || icons.pages;
}
function renderProfileMetricStrip(stats) {
  const host = $('profileMetricStrip');
  if (!host) return;
  const metrics = [
    { key: 'manuals', label: 'Manuals Read', value: stats.manuals },
    { key: 'strategy', label: 'Strategy Guides Read', value: stats.strategyGuides },
    { key: 'magazines', label: 'Magazines Read', value: stats.magazines },
    { key: 'pages', label: 'Pages Read', value: stats.pages.toLocaleString() },
    { key: 'words', label: 'Words Read', value: stats.words.toLocaleString() },
    { key: 'publishers', label: 'Authors / Publishers', value: stats.authors },
    { key: 'favorites', label: 'Favorites', value: stats.ratings }
  ];
  host.innerHTML = metrics.map(metric => `<div class="profile-metric profile-metric-${escapeForAttribute(metric.key)}"><i>${profileMetricIcon(metric.key)}</i><span>${escapeHtml(metric.label)}</span><strong>${escapeHtml(String(metric.value))}</strong></div>`).join('');
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
      cells += `<span class="profile-heatmap-cell" data-level="${level}" data-in-year="${inYear ? 'true' : 'false'}" title="${escapeForAttribute(label)}"></span>`;
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
  const recent = [...new Set(stats.allEvents.slice().reverse().map(e => String(e.id || '')).filter(Boolean))]
    .map(id => stats.lookup.get(id)).filter(Boolean).slice(0, 6);
  host.innerHTML = recent.length ? recent.map(item => `<article class="profile-recent-card"><img loading="lazy" src="${coverUrl(item)}" alt="" /><div><strong>${escapeHtml(displayTitle(item))}</strong><span>${escapeHtml(item.kind || '')} \u2022 ${escapeHtml(preferredPlatformOf(item) || categoryOf(item) || '\u2014')}</span></div></article>`).join('') : '<article class="settings-card"><p class="sub">Open a manual, guide, or magazine to start filling out recent reads.</p></article>';
}
function profileTopReadRows(stats) {
  const map = new Map();
  (stats.events || []).forEach(e => {
    const id = String(e.id || '').trim();
    const key = id || `title:${e.title || 'Unknown item'}`;
    const item = id ? stats.lookup.get(id) : null;
    const existing = map.get(key) || { key, id, item, title: item ? displayTitle(item) : (e.title || 'Unknown item'), kind: item?.kind || e.kind || 'Item', count: 0 };
    existing.count += 1;
    map.set(key, existing);
  });
  return [...map.values()].sort((a, b) => b.count - a.count || compareTextForSort(a.title, b.title));
}
function profileTopReadsHtml(stats) {
  const rows = profileTopReadRows(stats).slice(0, 8);
  if (!rows.length) return '<p class="sub">No read history yet.</p>';
  return rows.map((row, index) => {
    const img = row.item ? `<img loading="lazy" src="${coverUrl(row.item)}" alt="" />` : '<span class="profile-stat-preview-fallback">GV</span>';
    return `<div class="profile-stat-preview-row"><span class="profile-stat-rank">${index + 1}</span>${img}<div><strong>${escapeHtml(row.title)}</strong><em>${escapeHtml(row.kind)} \u2022 ${row.count} read${row.count === 1 ? '' : 's'}</em></div></div>`;
  }).join('');
}
function renderProfileTopLists(stats) {
  const host = $('profileTopLists');
  if (!host) return;
  host.innerHTML = [
    `<article class="settings-card profile-top-card profile-top-card-featured"><h2>Top Reads</h2>${profileTopReadsHtml(stats)}</article>`,
    `<article class="settings-card profile-top-card"><h2>By Content Type</h2>${topItemsHtml(countBy(stats.events, e => normalizeReadingKindGroup(e.kind) || 'Unknown'), 'events', 8)}</article>`,
    `<article class="settings-card profile-top-card"><h2>Recent Platforms</h2>${topItemsHtml(countBy(stats.uniqueItems, item => libraryCategoryKeysForItem(item)), 'items', 8)}</article>`
  ].join('');
}
function readProfileReviews() {
  try { return JSON.parse(localStorage.getItem(GUIDEVAULT_PROFILE_REVIEWS_KEY) || '[]').filter(Boolean); } catch { return []; }
}
function saveProfileReviews(reviews = []) {
  const clean = Array.isArray(reviews) ? reviews.filter(Boolean).slice(-500) : [];
  try { localStorage.setItem(GUIDEVAULT_PROFILE_REVIEWS_KEY, JSON.stringify(clean)); } catch {}
  return clean;
}
function profileReviewsForCurrentUser() {
  const userKey = profileUserKey();
  return readProfileReviews().filter(review => String(review.user || userKey).toLowerCase() === userKey);
}
function profileRecentReviewableItems(stats, limit = 12) {
  const seen = new Set();
  const ids = [];
  const source = (stats.allEvents || stats.events || []).slice().reverse();
  source.forEach(event => {
    const id = String(event.id || '').trim();
    if (!id || seen.has(id)) return;
    const item = stats.lookup.get(id);
    if (!item) return;
    seen.add(id);
    ids.push(item);
  });
  return ids.slice(0, limit);
}

function profileReviewKindWord(itemOrKind = null) {
  const kind = typeof itemOrKind === 'string' ? itemOrKind : String(itemOrKind?.kind || itemOrKind?.Kind || '').trim();
  const normalized = normalizeReadingKindGroup(kind) || kind;
  if (normalized === 'Manual') return 'Manual';
  if (normalized === 'Magazine') return 'Magazine';
  if (normalized === 'Strategy Guide') return 'Guide';
  return 'Item';
}
function profileReviewRatingLabel(itemOrKind = null) {
  return `${profileReviewKindWord(itemOrKind)} Rating`;
}
function profileReviewCleanSequenceValue(value, title = '') {
  let text = String(value || '').trim();
  if (!text) return '';
  const titleText = String(title || '').trim();
  if (titleText && text.toLowerCase().startsWith(titleText.toLowerCase())) {
    text = text.slice(titleText.length).trim();
  }
  text = text
    .replace(/^[\s:;\-\u2013\u2014\u2022]+/, '')
    .replace(/^(issue|iss\.?|no\.?|number|num\.?|#)\s*#?/i, '')
    .replace(/^[\s:;\-\u2013\u2014\u2022#]+/, '')
    .trim();
  const hashMatch = text.match(/#\s*([0-9]+[A-Za-z]?)\b/);
  if (hashMatch) text = hashMatch[1];
  const trailingNumber = text.match(/\b([0-9]+[A-Za-z]?)\s*$/);
  if (trailingNumber && /[A-Za-z]/.test(text.replace(trailingNumber[1], ''))) text = trailingNumber[1];
  if (/^0+\d+$/.test(text)) text = String(Number(text));
  return text.trim();
}
function profileReviewItemSequenceText(item = {}) {
  if (!item || typeof item !== 'object') return '';
  const normalizedKind = normalizeReadingKindGroup(item.kind || item.Kind || '') || String(item.kind || item.Kind || '').trim();
  if (normalizedKind !== 'Magazine') return '';
  const title = displayTitle(item) || '';
  const issue = profileReviewCleanSequenceValue(item.issueNumber || item.IssueNumber || '', title);
  const volume = profileReviewCleanSequenceValue(item.volume || item.Volume || '', title);
  const number = profileReviewCleanSequenceValue(item.number || item.Number || item.issueInVolume || '', title);
  if (volume && number) return `Vol. ${volume} No. ${number}`;
  if (volume && issue) return `Vol. ${volume} \u2022 Issue #${issue}`;
  if (issue) return `Issue #${issue}`;
  if (number) return `Issue #${number}`;
  return '';
}
function profileReviewItemYearText(item = {}) {
  const value = String(item.year || item.Year || item.coverDate || item.CoverDate || item.publicationDate || '').trim();
  const year = value.match(/\b(19|20)\d{2}\b/)?.[0] || '';
  return year;
}
function profileReviewItemOptionLabel(item = {}) {
  const title = displayTitle(item) || 'Untitled item';
  const sequence = profileReviewItemSequenceText(item);
  return sequence ? `${title} - ${sequence}` : title;
}
function profileReviewSelectedPreviewHtml(item = null) {
  if (!item) {
    return `<div class="profile-review-selected-preview is-empty"><div class="profile-review-selected-cover fallback">GV</div><div><strong>No recent item selected</strong><span>Open something in the reader first.</span></div></div>`;
  }
  const title = displayTitle(item) || 'Untitled item';
  const kindWord = profileReviewKindWord(item);
  const sequence = profileReviewItemSequenceText(item);
  const year = profileReviewItemYearText(item);
  const meta = [kindWord, sequence, year].filter(Boolean).join(' \u2022 ');
  const img = `<img src="${escapeForAttribute(coverUrl(item, { width: 280 }))}" alt="" loading="eager" />`;
  return `<div class="profile-review-selected-preview" id="profileReviewSelectedPreviewCard">
    <div class="profile-review-selected-cover">${img}</div>
    <div class="profile-review-selected-copy"><span>Reviewing</span><strong>${escapeHtml(title)}</strong>${meta ? `<em>${escapeHtml(meta)}</em>` : ''}</div>
  </div>`;
}
function updateProfileReviewSelectedPreview(item = null) {
  const host = $('profileReviewSelectedPreview');
  if (host) host.innerHTML = profileReviewSelectedPreviewHtml(item);
  const label = $('profileReviewRatingLabel');
  if (label) label.textContent = profileReviewRatingLabel(item);
}
function clampReviewRating(value, fallback = 0) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return Math.max(0, Math.min(5, Number(fallback || 0)));
  return Math.max(0, Math.min(5, parsed));
}
function profileReviewStars(value) {
  const rating = Math.round(clampReviewRating(value, 0));
  return rating ? '\u2605'.repeat(rating) + '\u2606'.repeat(5 - rating) : 'No rating';
}
function reviewRatingTone(value) {
  const rating = clampReviewRating(value, 0);
  if (!rating) return { hue: 208, color: 'hsl(208 16% 66%)' };
  const hue = Math.round(((rating - 1) / 4) * 120);
  return { hue, color: `hsl(${hue} 86% 58%)` };
}
function reviewRatingStyleAttr(value) {
  const tone = reviewRatingTone(value);
  const style = [
    `--review-rating-hue:${tone.hue}`,
    `--review-rating-color:${tone.color}`,
    `--review-rating-soft:hsla(${tone.hue},86%,58%,.16)`,
    `--review-rating-glow:hsla(${tone.hue},86%,58%,.30)`
  ].join(';');
  return ` style="${escapeForAttribute(style)}"`;
}
function reviewRatingStarsHtml(value, className = 'review-rating-stars') {
  return `<span class="${escapeForAttribute(className)}"${reviewRatingStyleAttr(value)}>${escapeHtml(profileReviewStars(value))}</span>`;
}
function normalizeReviewVisibility(value) {
  return String(value || '').trim().toLowerCase() === 'public' ? 'public' : 'private';
}
function isPublicReview(review) {
  return normalizeReviewVisibility(review?.visibility) === 'public' || review?.isPublic === true;
}
function profileReviewVisibilityLabel(review) {
  return isPublicReview(review) ? 'Public' : 'Private';
}
function currentProfileDisplayName() {
  const profile = state.auth.profile || readLoginProfile() || {};
  return String(profile.username || profile.email || 'Guidevault user').trim() || 'Guidevault user';
}
function currentProfileAvatarDataUrl() {
  const profile = state.auth.profile || readLoginProfile() || {};
  return String(profile.avatarDataUrl || '').trim();
}
function reviewAuthorLabel(review = {}) {
  return String(review.userDisplayName || review.author || review.user || 'Guidevault user').trim() || 'Guidevault user';
}
function reviewAuthorAvatarDataUrl(review = {}) {
  const direct = String(review.avatarDataUrl || review.userAvatarDataUrl || review.authorAvatarDataUrl || '').trim();
  if (direct) return direct;
  const user = String(review.user || '').trim().toLowerCase();
  if (user && user === profileUserKey()) return currentProfileAvatarDataUrl();
  return '';
}
function detailReviewAvatarHtml(review = {}) {
  const avatar = reviewAuthorAvatarDataUrl(review);
  const author = reviewAuthorLabel(review);
  if (avatar) {
    return `<div class="detail-review-avatar has-image" title="${escapeForAttribute(author)}"><img src="${escapeForAttribute(avatar)}" alt="" loading="lazy" /></div>`;
  }
  return `<div class="detail-review-avatar is-generic" title="${escapeForAttribute(author)}" aria-hidden="true"><svg viewBox="0 0 24 24" focusable="false"><path d="M12 12.4c2.4 0 4.35-1.95 4.35-4.35S14.4 3.7 12 3.7 7.65 5.65 7.65 8.05 9.6 12.4 12 12.4Zm0 2.1c-3.45 0-6.45 1.72-7.95 4.28-.48.82.12 1.82 1.07 1.82h13.76c.95 0 1.55-1 1.07-1.82-1.5-2.56-4.5-4.28-7.95-4.28Z"/></svg></div>`;
}
function publicReviewsForItemFromLocal(itemId) {
  const id = String(itemId || '');
  if (!id) return [];
  return readProfileReviews().filter(review => String(review.itemId || '') === id && isPublicReview(review));
}
function mergeReviewLists(...lists) {
  const map = new Map();
  lists.flat().filter(Boolean).forEach(review => {
    const key = String(review.id || `${review.user || 'user'}:${review.itemId || ''}:${review.updatedAt || review.createdAt || ''}`);
    map.set(key, { ...review, visibility: normalizeReviewVisibility(review.visibility), rating: Math.max(1, Math.min(5, Number(review.rating || 5))) });
  });
  return [...map.values()].sort((a, b) => (dateValue(b.updatedAt || b.createdAt) || 0) - (dateValue(a.updatedAt || a.createdAt) || 0));
}
function renderProfileReviews(stats) {
  const host = $('profileReviewsContent');
  if (!host) return;
  const recentItems = profileRecentReviewableItems(stats);
  const reviews = profileReviewsForCurrentUser().sort((a, b) => (dateValue(b.updatedAt || b.createdAt) || 0) - (dateValue(a.updatedAt || a.createdAt) || 0));
  const previousSelected = $('profileReviewItemSelect')?.value || '';
  const selectedItem = recentItems.find(item => String(item.id || item.Id || '') === previousSelected) || recentItems[0] || null;
  const selectedId = String(selectedItem?.id || selectedItem?.Id || '');
  const existing = selectedId ? reviews.find(review => String(review.itemId || '') === selectedId) : null;
  const options = recentItems.map(item => `<option value="${escapeForAttribute(item.id || item.Id || '')}">${escapeHtml(profileReviewItemOptionLabel(item))}</option>`).join('');
  const list = reviews.length ? reviews.map(review => {
    const item = review.itemId ? stats.lookup.get(String(review.itemId)) : null;
    const title = item ? displayTitle(item) : (review.title || 'Unknown item');
    const kind = item?.kind || review.kind || 'Item';
    const cover = item ? `<img loading="lazy" src="${coverUrl(item)}" alt="" />` : '<span class="profile-stat-preview-fallback">GV</span>';
    return `<article class="profile-review-card">${cover}<div><div class="profile-review-card-head"><strong>${escapeHtml(title)}</strong>${reviewRatingStarsHtml(review.rating, 'profile-review-rating')}</div><div class="profile-review-visibility ${isPublicReview(review) ? 'public' : 'private'}">${escapeHtml(profileReviewVisibilityLabel(review))} review</div><p>${escapeHtml(review.text || '')}</p><em>${escapeHtml(kind)} \u2022 updated ${escapeHtml(formatProfileDateWithRelative(review.updatedAt || review.createdAt))}</em><button class="ghost mini profile-review-delete" type="button" data-profile-review-delete="${escapeForAttribute(review.id)}">Delete review</button></div></article>`;
  }).join('') : '<article class="settings-card"><p class="sub">No reviews yet. Choose a recently read item and write the first one.</p></article>';
  host.innerHTML = `
    <div class="profile-review-layout">
      <article class="settings-card profile-review-editor">
        <h2>Write a Review</h2>
        <div id="profileReviewSelectedPreview">${profileReviewSelectedPreviewHtml(selectedItem)}</div>
        ${recentItems.length ? `
          <label>Recently read item<select id="profileReviewItemSelect">${options}</select></label>
          <label><span id="profileReviewRatingLabel">${escapeHtml(profileReviewRatingLabel(selectedItem))}</span><select id="profileReviewRating">
            <option value="5">5 - Loved it</option>
            <option value="4">4 - Strong</option>
            <option value="3">3 - Good enough</option>
            <option value="2">2 - Rough</option>
            <option value="1">1 - Not useful</option>
          </select></label>
          <label>Visibility<select id="profileReviewVisibility">
            <option value="private">Private - only show in my profile</option>
            <option value="public">Public - show on the item Reviews tab</option>
          </select></label>
          <label>Review<textarea id="profileReviewText" rows="7" maxlength="2000" placeholder="What stood out? Was it complete, useful, nostalgic, or hard to read?">${escapeHtml(existing?.text || '')}</textarea></label>
          <div class="profile-review-actions"><button id="profileSaveReview" class="primary" type="button">Save Review</button><button id="profileClearReview" class="ghost" type="button">Clear</button></div>
          <p id="profileReviewStatus" class="sub"></p>
        ` : '<p class="sub">Open something in the reader first. Recent reads will appear here as review options.</p>'}
      </article>
      <div class="profile-review-list">${list}</div>
    </div>`;
  const select = $('profileReviewItemSelect');
  if (select && selectedId) select.value = selectedId;
  const rating = $('profileReviewRating');
  if (rating) rating.value = String(existing?.rating || 5);
  const visibility = $('profileReviewVisibility');
  if (visibility) visibility.value = normalizeReviewVisibility(existing?.visibility || (existing?.isPublic ? 'public' : 'private'));
  updateProfileReviewSelectedPreview(selectedItem);
}
function syncProfileReviewEditorFromSelection() {
  const stats = profilePageStats();
  const itemId = $('profileReviewItemSelect')?.value || '';
  const item = itemId ? stats.lookup.get(String(itemId)) : null;
  const existing = profileReviewsForCurrentUser().find(review => String(review.itemId || '') === String(itemId));
  updateProfileReviewSelectedPreview(item);
  if ($('profileReviewRating')) $('profileReviewRating').value = String(existing?.rating || 5);
  if ($('profileReviewVisibility')) $('profileReviewVisibility').value = normalizeReviewVisibility(existing?.visibility || (existing?.isPublic ? 'public' : 'private'));
  if ($('profileReviewText')) $('profileReviewText').value = existing?.text || '';
  if ($('profileReviewStatus')) $('profileReviewStatus').textContent = existing ? 'Loaded your saved review for this item.' : '';
}
async function saveProfileReviewFromForm() {
  const select = $('profileReviewItemSelect');
  const itemId = select?.value || '';
  const rating = Math.max(1, Math.min(5, Number($('profileReviewRating')?.value || 5)));
  const visibility = normalizeReviewVisibility($('profileReviewVisibility')?.value || 'private');
  const text = String($('profileReviewText')?.value || '').trim();
  const status = $('profileReviewStatus');
  if (!itemId) { if (status) status.textContent = 'Choose a recently read item first.'; return; }
  if (!text) { if (status) status.textContent = 'Write a short review before saving.'; return; }
  const item = (state.items || []).find(i => String(i.id || i.Id || '') === String(itemId)) || null;
  const user = profileUserKey();
  const now = new Date().toISOString();
  const reviews = readProfileReviews();
  const existingIndex = reviews.findIndex(review => String(review.user || '').toLowerCase() === user && String(review.itemId || '') === String(itemId));
  const nextReview = {
    id: existingIndex >= 0 ? reviews[existingIndex].id : `review-${Date.now()}-${Math.random().toString(16).slice(2)}`,
    user,
    userDisplayName: currentProfileDisplayName(),
    avatarDataUrl: currentProfileAvatarDataUrl(),
    itemId,
    title: item ? displayTitle(item) : (select?.selectedOptions?.[0]?.textContent || 'Unknown item'),
    kind: item?.kind || '',
    issueNumber: item?.issueNumber || item?.IssueNumber || '',
    volume: item?.volume || item?.Volume || '',
    number: item?.number || item?.Number || '',
    rating,
    text,
    visibility,
    isPublic: visibility === 'public',
    createdAt: existingIndex >= 0 ? reviews[existingIndex].createdAt : now,
    updatedAt: now
  };
  if (existingIndex >= 0) reviews[existingIndex] = nextReview;
  else reviews.push(nextReview);
  saveProfileReviews(reviews);
  let reviewStatusMessage = '';
  try {
    const synced = await syncProfileReviewToServer(nextReview);
    if (synced?.review) {
      const refreshed = readProfileReviews();
      const idx = refreshed.findIndex(review => String(review.id || '') === String(nextReview.id || ''));
      if (idx >= 0) {
        refreshed[idx] = { ...refreshed[idx], ...synced.review, itemId: synced.review.itemId || nextReview.itemId };
        saveProfileReviews(refreshed);
      }
      state.itemReviews.cache[String(itemId)] = Array.isArray(synced.reviews) ? synced.reviews : state.itemReviews.cache[String(itemId)];
    }
    reviewStatusMessage = visibility === 'public' ? 'Public review saved and available on the item Reviews tab.' : 'Private review saved to your profile.';
  } catch {
    reviewStatusMessage = 'Review saved locally. Server review sync is unavailable.';
  }
  renderProfileReviews(profilePageStats());
  if ($('profileReviewStatus')) $('profileReviewStatus').textContent = reviewStatusMessage;
  if (state.selected && String(itemIdOf(state.selected)) === String(itemId)) renderDetailReviews(state.selected);
}
function clearProfileReviewForm() {
  if ($('profileReviewText')) $('profileReviewText').value = '';
  if ($('profileReviewRating')) $('profileReviewRating').value = '5';
  if ($('profileReviewVisibility')) $('profileReviewVisibility').value = 'private';
  if ($('profileReviewStatus')) $('profileReviewStatus').textContent = '';
}
async function deleteProfileReview(reviewId) {
  const id = String(reviewId || '');
  if (!id) return;
  const existing = readProfileReviews().find(review => String(review.id || '') === id);
  saveProfileReviews(readProfileReviews().filter(review => String(review.id || '') !== id));
  if (existing?.itemId) state.itemReviews.cache[String(existing.itemId)] = (state.itemReviews.cache[String(existing.itemId)] || []).filter(review => String(review.id || '') !== id);
  try { await fetch(`/api/reviews/${encodeURIComponent(id)}?user=${encodeURIComponent(profileUserKey())}`, { method: 'DELETE' }); } catch {}
  renderProfileReviews(profilePageStats());
  if (state.selected && existing?.itemId && String(itemIdOf(state.selected)) === String(existing.itemId)) renderDetailReviews(state.selected);
}
async function syncProfileReviewToServer(review) {
  const itemId = String(review?.itemId || '');
  if (!itemId) return null;
  const response = await fetch(`/api/items/${encodeURIComponent(itemId)}/reviews`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      id: review.id,
      user: review.user || profileUserKey(),
      userDisplayName: review.userDisplayName || currentProfileDisplayName(),
      avatarDataUrl: review.avatarDataUrl || currentProfileAvatarDataUrl(),
      title: review.title || '',
      kind: review.kind || '',
      issueNumber: review.issueNumber || '',
      volume: review.volume || '',
      number: review.number || '',
      rating: review.rating || 5,
      text: review.text || '',
      visibility: normalizeReviewVisibility(review.visibility)
    })
  });
  if (!response.ok) throw new Error('Review sync failed');
  return response.json();
}
function publicReviewsForItem(itemId) {
  const id = String(itemId || '');
  return mergeReviewLists(state.itemReviews.cache[id] || [], publicReviewsForItemFromLocal(id));
}
function detailReviewSummaryHtml(reviews) {
  if (!reviews.length) {
    return `
      <div class="detail-review-score-main is-empty"><strong>\u2014</strong><span>No rating yet</span></div>
      <div class="detail-review-score-context"><span>No reviews yet</span><em>Be the first to add a score from your profile.</em></div>`;
  }
  const avg = reviews.reduce((sum, review) => sum + clampReviewRating(review.rating, 5), 0) / reviews.length;
  const avgText = avg.toFixed(1);
  return `
    <div class="detail-review-score-main"${reviewRatingStyleAttr(avg)}><strong>${avgText}</strong><span>average rating</span></div>
    <div class="detail-review-score-context"${reviewRatingStyleAttr(avg)}>${reviewRatingStarsHtml(avg, 'detail-review-summary-stars')}<em>Based on ${reviews.length} public review${reviews.length === 1 ? '' : 's'}</em></div>`;
}
function detailPublicReviewsHtml(item, reviews) {
  if (!reviews.length) {
    return `<article class="detail-review-empty"><h3>No reviews yet</h3><p class="sub">Shared profile reviews for ${escapeHtml(displayTitle(item))} will appear here.</p></article>`;
  }
  return reviews.map(review => {
    const author = reviewAuthorLabel(review);
    const date = formatProfileDateWithRelative(review.updatedAt || review.createdAt);
    return `<article class="detail-review-card">${detailReviewAvatarHtml(review)}<div class="detail-review-main"><div class="detail-review-head"><div><strong>${escapeHtml(author)}</strong><em>${escapeHtml(date)}</em></div>${reviewRatingStarsHtml(review.rating, 'detail-review-rating')}</div><p>${escapeHtml(review.text || '')}</p></div></article>`;
  }).join('');
}
function renderDetailReviews(item, loading = false) {
  const host = $('detailReviewsContent');
  if (!host) return;
  const itemId = itemIdOf(item);
  const reviews = publicReviewsForItem(itemId);
  host.innerHTML = `
    <div class="detail-reviews-header">
      <div class="detail-reviews-summary">${detailReviewSummaryHtml(reviews)}</div>
    </div>
    ${loading ? '<p class="sub detail-review-loading">Refreshing reviews...</p>' : ''}
    <div class="detail-review-list">${detailPublicReviewsHtml(item, reviews)}</div>`;
}
async function loadPublicReviewsForItem(item, force = false) {
  const itemId = itemIdOf(item);
  if (!itemId) return;
  if (!force && Array.isArray(state.itemReviews.cache[itemId])) {
    renderDetailReviews(item);
    return;
  }
  if (state.itemReviews.loading[itemId]) return;
  state.itemReviews.loading[itemId] = true;
  renderDetailReviews(item, true);
  try {
    const response = await fetch(`/api/items/${encodeURIComponent(itemId)}/reviews`, { cache: 'no-store' });
    if (response.ok) {
      const data = await response.json();
      state.itemReviews.cache[itemId] = Array.isArray(data.reviews) ? data.reviews : [];
    }
  } catch {}
  finally {
    state.itemReviews.loading[itemId] = false;
    if (state.selected && itemIdOf(state.selected) === itemId) renderDetailReviews(state.selected);
  }
}
function renderProfileActivityList(stats) {
  const host = $('profileActivityList');
  if (!host) return;
  const total = (stats.events || []).length;
  const rows = (stats.events || []).slice().reverse().slice(0, GUIDEVAULT_PROFILE_ACTIVITY_DISPLAY_LIMIT);
  const note = total > rows.length
    ? `<div class="profile-activity-limit-note">Showing the latest ${rows.length.toLocaleString()} of ${total.toLocaleString()} activity entries for this range. Local activity storage is capped at ${GUIDEVAULT_READING_ACTIVITY_LIMIT.toLocaleString()} entries.</div>`
    : `<div class="profile-activity-limit-note">Local activity storage is capped at ${GUIDEVAULT_READING_ACTIVITY_LIMIT.toLocaleString()} entries to keep the profile page fast.</div>`;
  host.innerHTML = rows.length
    ? `${note}${rows.map(e => `<article class="settings-card profile-activity-row"><strong>${escapeHtml(e.title || 'Unknown item')}</strong><span>${escapeHtml(e.kind || 'Item')} \u2022 ${escapeHtml(e.action || 'read')}</span><em>${escapeHtml(e.at ? new Date(e.at).toLocaleString() : 'Unknown time')}</em></article>`).join('')}`
    : '<article class="settings-card"><p class="sub">No activity is logged for this range yet.</p></article>';
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
  renderProfileHeroBackground(profile);
  setText('profilePageName', profile.username || profile.email || 'Guidevault User');
  setText('profileReadBadge', `${stats.allReadCount} Read${stats.allReadCount === 1 ? '' : 's'}`);
  setText('profileJoined', stats.joined ? formatProfileDateWithRelative(stats.joined) : '\u2014');
  setText('profileLastRead', stats.lastRead ? formatProfileDateWithRelative(stats.lastRead) : 'No reads yet');
  setText('profileTotalReadTime', formatProfileMinutes(stats.allEstimatedMinutes));
  setText('profileAvgPerWeek', `${formatProfileMinutes(stats.avgPerWeek)} / week`);
  setText('profileOverviewTitle', `A look at ${(profile.username || 'your')}\u2019s journey through Guidevault`);
  if ($('profileRange')) $('profileRange').value = state.profilePage.range || 'all';
  renderProfileMetricStrip(stats);
  renderProfileHeatmap(stats);
  renderProfileRecent(stats);
  renderProfileTopLists(stats);
  renderProfileReviews(stats);
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
  loadCustomizeSettings();
  renderCustomSideNavItems();
  syncCustomizeSettingsFromServer(true);
  renderAccountProfile();
  syncTopUserMenu();
  startDeviceHeartbeat();
  resetGuidevaultLandingToHome({ render: false });
  if (!state.auth.appStarted) {
    state.auth.appStarted = true;
    renderCachedLibraryImmediately();
    loadLibrary();
    startStableUpdatePolling();
  } else {
    applyFilters();
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
  if (!existing) {
    const validation = validateLoginProfile(form);
    if (validation) { setLoginStatus(validation, 'error'); return; }
    saveLoginProfile(form);
    setLoginStatus('Local login created.', 'success');
    showAuthenticatedApp();
    return;
  }

  const validation = validateExistingLoginCredentials(form);
  if (validation) { setLoginStatus(validation, 'error'); return; }
  const matches = profileMatchesLoginIdentity(existing, form.identity) && form.password === existing.password;
  if (!matches) {
    setLoginStatus('The username / email address or password does not match the saved local profile.', 'error');
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
  return {
    useColorscape: false,
    colorscapeDetailPane: true,
    colorscapeManualMenus: false,
    colorscapeStrategyMenus: false,
    colorscapeMagazineMenus: false
  };
}

function preferenceBool(value, fallback = false) {
  if (value === undefined || value === null || value === '') return !!fallback;
  if (typeof value === 'boolean') return value;
  const text = String(value).trim().toLowerCase();
  if (['true','1','yes','on','enabled'].includes(text)) return true;
  if (['false','0','no','off','disabled'].includes(text)) return false;
  return !!fallback;
}

function normalizeGuidevaultPreferences(value = {}) {
  const defaults = defaultGuidevaultPreferences();
  return {
    useColorscape: preferenceBool(value.useColorscape, defaults.useColorscape),
    colorscapeDetailPane: preferenceBool(value.colorscapeDetailPane, defaults.colorscapeDetailPane),
    colorscapeManualMenus: preferenceBool(value.colorscapeManualMenus, defaults.colorscapeManualMenus),
    colorscapeStrategyMenus: preferenceBool(value.colorscapeStrategyMenus, defaults.colorscapeStrategyMenus),
    colorscapeMagazineMenus: preferenceBool(value.colorscapeMagazineMenus, defaults.colorscapeMagazineMenus)
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
  const masterEnabled = !!preferences.useColorscape;
  if ($('preferenceUseColorscape')) $('preferenceUseColorscape').checked = masterEnabled;
  [
    ['preferenceColorscapeDetailPane', 'colorscapeDetailPane'],
    ['preferenceColorscapeManualMenus', 'colorscapeManualMenus'],
    ['preferenceColorscapeStrategyMenus', 'colorscapeStrategyMenus'],
    ['preferenceColorscapeMagazineMenus', 'colorscapeMagazineMenus']
  ].forEach(([id, key]) => {
    const input = $(id);
    if (!input) return;
    input.checked = !!preferences[key];
    input.disabled = !masterEnabled;
  });
  const card = document.querySelector('.preferences-card');
  if (card) card.classList.toggle('colorscape-disabled', !masterEnabled);
  if (document.body.classList.contains('detail-page-mode')) applyColorscapeToDetail(state.selected);
  refreshColorscapeGroupCards();
}

function colorscapePreferenceLabel(key) {
  return ({
    useColorscape: 'Colorscape',
    colorscapeDetailPane: 'Details pane Colorscape',
    colorscapeManualMenus: 'Manual menu container Colorscape',
    colorscapeStrategyMenus: 'Strategy guide menu container Colorscape',
    colorscapeMagazineMenus: 'Magazine menu container Colorscape'
  })[key] || 'Colorscape';
}

function setGuidevaultPreferenceValue(key, enabled) {
  const current = state.preferences || loadGuidevaultPreferences();
  const next = saveGuidevaultPreferences({ ...current, [key]: !!enabled });
  renderPreferencesSettings();
  const label = colorscapePreferenceLabel(key);
  const masterOff = !next.useColorscape;
  if (key === 'useColorscape') {
    setPreferencesStatus(enabled ? 'Colorscape enabled. Child toggles now control where the cover-color effect appears.' : 'Colorscape disabled. Theme gradients will be used everywhere.', enabled ? 'success' : 'info');
  } else {
    setPreferencesStatus(`${label} ${enabled ? 'enabled' : 'disabled'}${masterOff ? ' \u2014 enable Use Colorscape to apply it.' : '.'}`, enabled ? 'success' : 'info');
  }
  if (document.body.classList.contains('detail-page-mode')) applyColorscapeToDetail(state.selected);
  else clearColorscapeDetailTheme();
  refreshColorscapeGroupCards();
}

function setUseColorscapePreference(enabled) {
  setGuidevaultPreferenceValue('useColorscape', enabled);
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
  const width = 32;
  const height = 44;
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

function loadColorscapeColorCache() {
  if (state.colorscape.persistentLoaded) return state.colorscape.cache || {};
  state.colorscape.persistentLoaded = true;
  state.colorscape.cache = state.colorscape.cache || {};
  try {
    const raw = localStorage.getItem(GUIDEVAULT_COLORSCAPE_CACHE_KEY);
    if (!raw) return state.colorscape.cache;
    const parsed = JSON.parse(raw);
    const colors = parsed?.colors && typeof parsed.colors === 'object' ? parsed.colors : parsed;
    Object.entries(colors || {}).forEach(([url, value]) => {
      const rgb = Array.isArray(value) ? value : value?.rgb;
      if (!url || !Array.isArray(rgb) || rgb.length < 3) return;
      state.colorscape.cache[url] = normalizeRgbTriplet(rgb);
    });
  } catch {
    state.colorscape.cache = state.colorscape.cache || {};
  }
  return state.colorscape.cache;
}

function persistColorscapeColorCache(immediate = false) {
  if (!state.colorscape.persistentLoaded) return;
  const save = () => {
    state.colorscape.cacheSaveTimer = 0;
    try {
      const entries = Object.entries(state.colorscape.cache || {}).filter(([, rgb]) => Array.isArray(rgb) && rgb.length >= 3);
      const trimmed = entries.slice(Math.max(0, entries.length - GUIDEVAULT_COLORSCAPE_CACHE_LIMIT));
      const colors = Object.fromEntries(trimmed.map(([url, rgb]) => [url, { rgb: normalizeRgbTriplet(rgb), savedAt: Date.now() }]));
      localStorage.setItem(GUIDEVAULT_COLORSCAPE_CACHE_KEY, JSON.stringify({ version: 1, colors }));
    } catch {}
  };
  if (immediate) {
    if (state.colorscape.cacheSaveTimer) clearTimeout(state.colorscape.cacheSaveTimer);
    save();
    return;
  }
  if (state.colorscape.cacheSaveTimer) return;
  state.colorscape.cacheSaveTimer = window.setTimeout(save, 650);
}

function getCachedDominantCoverColor(url) {
  if (!url) return null;
  const cache = loadColorscapeColorCache();
  const rgb = cache?.[url];
  return Array.isArray(rgb) && rgb.length >= 3 ? normalizeRgbTriplet(rgb) : null;
}

function setCachedDominantCoverColor(url, rgb) {
  if (!url) return normalizeRgbTriplet(rgb);
  const normalized = normalizeRgbTriplet(rgb);
  loadColorscapeColorCache();
  state.colorscape.cache[url] = normalized;
  persistColorscapeColorCache(false);
  return normalized;
}

function fastColorscapeFallbackColor(seed) {
  const text = String(seed || 'Guidevault');
  let hash = 2166136261;
  for (let i = 0; i < text.length; i += 1) {
    hash ^= text.charCodeAt(i);
    hash = Math.imul(hash, 16777619);
  }
  const hue = ((hash >>> 0) % 360) / 360;
  return hslToRgb(hue, .58, .44);
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

function imageElementMatchesColorscapeUrl(img, url) {
  if (!img || !url) return false;
  const current = img.currentSrc || img.getAttribute('src') || '';
  const wanted = img.dataset.coverSrc || url;
  return wanted === url || current === url || current.endsWith(url) || wanted.endsWith(url);
}

function findLoadedColorscapeImage(card, url) {
  if (!card || !url) return null;
  const images = Array.from(card.querySelectorAll('img[data-cover-src], img[src]'));
  return images.find(img => imageElementMatchesColorscapeUrl(img, url) && img.complete && img.naturalWidth > 1 && img.naturalHeight > 1) || null;
}

async function getDominantCoverColor(url, imageEl = null) {
  if (!url) return [88, 151, 255];
  const cached = getCachedDominantCoverColor(url);
  if (cached) return cached;
  try {
    const source = imageEl && imageEl.complete && imageEl.naturalWidth > 1 && imageEl.naturalHeight > 1
      ? imageEl
      : await loadImageForColorscape(url);
    return setCachedDominantCoverColor(url, sampleDominantCoverColor(source));
  } catch (err) {
    console.warn('Colorscape sampling failed', err);
    return [88, 151, 255];
  }
}

async function applyColorscapeToDetail(item) {
  const preferences = state.preferences || loadGuidevaultPreferences();
  if (!preferences.useColorscape || !preferences.colorscapeDetailPane || !isColorscapeSupportedItem(item)) {
    clearColorscapeDetailTheme();
    return;
  }
  const itemId = String(item?.id || item?.Id || '');
  const url = coverUrl(item);
  const token = (state.colorscape.token || 0) + 1;
  state.colorscape.token = token;
  // Apply cached/fallback color immediately so the page does not flash back to the theme gradient.
  const cached = getCachedDominantCoverColor(url);
  applyColorscapeRgb(cached || fastColorscapeFallbackColor(`${item?.kind || ''}:${displayTitle(item)}`), item);
  const rgb = cached || await getDominantCoverColor(url);
  if (state.colorscape.token !== token) return;
  if (!document.body.classList.contains('detail-page-mode')) return;
  if (String(state.selected?.id || state.selected?.Id || '') !== itemId) return;
  applyColorscapeRgb(rgb, item);
}


function colorscapeMenuPreferenceKeyForKind(kind) {
  const normalized = String(kind || '').trim();
  if (normalized === 'Manual') return 'colorscapeManualMenus';
  if (normalized === 'Strategy Guide') return 'colorscapeStrategyMenus';
  if (normalized === 'Magazine') return 'colorscapeMagazineMenus';
  return '';
}

function isColorscapeMenuEnabledForKind(kind) {
  const preferences = state.preferences || loadGuidevaultPreferences();
  const key = colorscapeMenuPreferenceKeyForKind(kind);
  return !!(preferences.useColorscape && key && preferences[key]);
}

function applyColorscapeRgbToCategoryCard(card, rgb) {
  if (!card) return;
  const [r, g, b] = normalizeRgbTriplet(rgb);
  card.style.setProperty('--category-colorscape-rgb', `${r}, ${g}, ${b}`);
  card.classList.remove('category-colorscape-pending');
  card.classList.add('category-colorscape-active');
}

function clearColorscapeCategoryCard(card) {
  if (!card) return;
  card.classList.remove('category-colorscape-active', 'category-colorscape-pending');
  card.style.removeProperty('--category-colorscape-rgb');
}

function clearColorscapeGroupCards(root = $('grid')) {
  if (!root) return;
  root.querySelectorAll('.category-card.category-card-redesign').forEach(clearColorscapeCategoryCard);
}

function refreshColorscapeGroupCards() {
  const groupMode = ['manual-systems', 'guide-systems', 'magazine-series'].includes(state.viewMode);
  if (!groupMode) return;
  scheduleApplyColorscapeToGroupCards(groupDefinition(state.viewMode).kind);
}

function colorscapeCardViewportDistance(card) {
  if (!card?.getBoundingClientRect) return 0;
  const scroller = libraryScrollElement();
  const viewport = scroller?.getBoundingClientRect?.() || { top: 0, bottom: window.innerHeight || 0 };
  const rect = card.getBoundingClientRect();
  if (rect.bottom >= viewport.top && rect.top <= viewport.bottom) return 0;
  return Math.min(Math.abs(rect.top - viewport.bottom), Math.abs(rect.bottom - viewport.top));
}

function runWhenBrowserIsIdle(work, timeout = 1200) {
  if (typeof requestIdleCallback === 'function') {
    requestIdleCallback(work, { timeout });
    return;
  }
  window.setTimeout(work, Math.min(timeout, 250));
}

function scheduleColorscapeSampleQueue() {
  if (state.colorscape.imageSampleQueued) return;
  state.colorscape.imageSampleQueued = true;
  runWhenBrowserIsIdle(async () => {
    state.colorscape.imageSampleQueued = false;
    const token = state.colorscape.menuToken;
    const batch = (state.colorscape.imageSampleQueue || []).splice(0, GUIDEVAULT_COLORSCAPE_MENU_BATCH_SIZE);
    for (const entry of batch) {
      const { card, img, kind, url } = entry;
      if (state.colorscape.menuToken !== token) return;
      if (!card?.isConnected || !img?.isConnected || !isColorscapeMenuEnabledForKind(kind)) continue;
      if (!img.complete || img.naturalWidth <= 1 || img.naturalHeight <= 1) continue;
      const rgb = await getDominantCoverColor(url, img);
      if (state.colorscape.menuToken !== token || !card.isConnected) return;
      applyColorscapeRgbToCategoryCard(card, rgb);
      await new Promise(resolve => requestAnimationFrame(resolve));
    }
    persistColorscapeColorCache(false);
    if ((state.colorscape.imageSampleQueue || []).length) scheduleColorscapeSampleQueue();
  }, GUIDEVAULT_COLORSCAPE_MENU_SAMPLE_DELAY_MS);
}

function scheduleColorscapeSampleForLoadedCover(img) {
  if (!img || !img.isConnected || !img.complete || img.naturalWidth <= 1 || img.naturalHeight <= 1) return;
  const card = img.closest?.('.category-card.category-card-redesign[data-colorscape-cover]');
  if (!card) return;
  const kind = card.dataset.kind || groupDefinition(state.viewMode).kind;
  if (!isColorscapeMenuEnabledForKind(kind)) return;
  const url = card.dataset.colorscapeCover || '';
  if (!url || !imageElementMatchesColorscapeUrl(img, url)) return;
  const queue = state.colorscape.imageSampleQueue || (state.colorscape.imageSampleQueue = []);
  if (!queue.some(entry => entry.card === card && entry.img === img)) queue.push({ card, img, kind, url });
  scheduleColorscapeSampleQueue();
}

function scheduleApplyColorscapeToGroupCards(kind) {
  const root = $('grid');
  if (!root) return;
  const token = (state.colorscape.menuToken || 0) + 1;
  state.colorscape.menuToken = token;
  state.colorscape.imageSampleQueue = [];
  state.colorscape.imageSampleQueued = false;
  if (!isColorscapeMenuEnabledForKind(kind)) {
    clearColorscapeGroupCards(root);
    return;
  }
  loadColorscapeColorCache();
  const cards = Array.from(root.querySelectorAll('.category-card.category-card-redesign[data-colorscape-cover]'))
    .filter(card => !!card.dataset.colorscapeCover)
    .sort((a, b) => colorscapeCardViewportDistance(a) - colorscapeCardViewportDistance(b));
  cards.forEach(card => {
    const url = card.dataset.colorscapeCover;
    const cached = getCachedDominantCoverColor(url);
    if (cached) {
      applyColorscapeRgbToCategoryCard(card, cached);
      return;
    }
    applyColorscapeRgbToCategoryCard(card, fastColorscapeFallbackColor(`${kind}:${card.dataset.category || url}`));
    card.classList.add('category-colorscape-pending');
    const loadedImage = findLoadedColorscapeImage(card, url);
    if (loadedImage) scheduleColorscapeSampleForLoadedCover(loadedImage);
  });
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
  loadColorscapeColorCache();
  loadGuidevaultPreferences();
  loadKeybinds();
  loadCustomizeSettings();
  syncCustomizeSettingsFromServer(false);
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
  if (!value) return '\u2014';
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
  const setText = (id, value) => { const el = $(id); if (el) el.textContent = value || '\u2014'; };
  setText('systemAppName', data.appName || 'Guidevault');
  setText('systemVersion', data.version || GUIDEVAULT_APP_VERSION);
  setText('systemFirstInstallVersion', data.firstInstallVersion || data.version || GUIDEVAULT_APP_VERSION);
  setText('systemFirstInstallDate', formatSystemDate(data.firstInstallDate));
  setText('systemInstallId', data.installId || '\u2014');
  setText('systemRuntimeMode', data.runtimeMode || 'Local self-hosted web app');
  setText('systemSupportedFiles', data.supportedFiles || 'CBZ, CBR, PDF');
  trimSystemUpdateHistory();
}

function trimSystemUpdateHistory(limit = 20) {
  const panel = $('settingsInfoPanel');
  if (!panel) return;
  const explanatory = panel.querySelector('.system-update-history-head .sub');
  if (explanatory) explanatory.remove();
  const entries = Array.from(panel.querySelectorAll('.system-update-entry'));
  entries.forEach((entry, index) => {
    if (index >= limit) entry.remove();
  });
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
  const setText = (id, value) => { const el = $(id); if (el) el.textContent = value || '\u2014'; };
  if (!data) {
    setText('systemWorkingSet', '\u2014');
    setText('systemPrivateMemory', '\u2014');
    setText('systemManagedMemory', '\u2014');
    setText('systemGcMode', '\u2014');
    setText('systemArchiveEntryCache', '\u2014');
    setText('systemCoverDiskCache', '\u2014');
    setText('systemCachedItems', '\u2014');
    setText('systemActiveTasks', '\u2014');
    setText('systemLastScan', '\u2014');
    return;
  }
  setText('systemWorkingSet', formatDiagnosticBytes(data.process?.workingSetBytes));
  setText('systemPrivateMemory', formatDiagnosticBytes(data.process?.privateMemoryBytes));
  setText('systemManagedMemory', formatDiagnosticBytes(data.dotnet?.totalManagedMemoryBytes));
  setText('systemGcMode', data.dotnet?.isServerGc ? 'Server GC' : 'Workstation GC');
  setText('systemArchiveEntryCache', `${Number(data.archive?.imageEntryCacheCount || 0)} archive index entr${Number(data.archive?.imageEntryCacheCount || 0) === 1 ? 'y' : 'ies'} \u2022 ${Number(data.archive?.inFlightCoverReads || 0)} cover read(s)`);
  setText('systemCoverDiskCache', `${Number(data.archive?.diskCoverCacheFiles || 0)} file(s) \u2022 ${formatDiagnosticBytes(data.archive?.diskCoverCacheBytes)}`);
  setText('systemCachedItems', `${Number(data.library?.cachedItemCount || 0)} item(s)`);
  setText('systemActiveTasks', `${Number(data.tasks?.activeCount || 0)} active \u2022 ${Number(data.tasks?.recentCount || 0)} recent`);
  const last = data.library?.lastScan || {};
  const elapsed = Number(last.elapsedMs || 0);
  const elapsedText = elapsed > 0 ? `${(elapsed / 1000).toFixed(1)}s` : '\u2014';
  setText('systemLastScan', last.message ? `${last.message} \u2022 ${elapsedText}` : 'No scan completed this session');
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
  const notice = $('systemUpdateNotice');
  if (notice) {
    notice.classList.remove('hidden');
    notice.dataset.status = update?.status || '';
  }
  const setText = (id, value) => { const el = $(id); if (el) el.textContent = value || '\u2014'; };
  const hasUpdatePaths = !!(update?.feedUrl || update?.releaseUrl || update?.releasePath || update?.packageUrl);
  const currentVersion = update?.currentVersion || GUIDEVAULT_APP_VERSION;
  const latestVersion = update?.latestVersion || '';
  const sameVersion = latestVersion && normalizeStableVersion(latestVersion) === normalizeStableVersion(currentVersion);
  const friendlyUpdateMessage = (hasUpdatePaths && sameVersion && String(update?.message || '').toLowerCase().includes('failed'))
    ? 'Guidevault is current. Release and package paths are available below.'
    : (update?.message || 'Stable update notifications are not configured yet.');
  setText('systemUpdateStatus', friendlyUpdateMessage);
  setText('systemUpdateCurrent', update?.currentVersion || GUIDEVAULT_APP_VERSION);
  setText('systemUpdateLatest', update?.latestVersion || '\u2014');
  setText('systemUpdateImage', update?.latestImage || update?.currentImage || '\u2014');
  setText('systemUpdateFeed', update?.feedUrl || '\u2014');
  setText('systemUpdateReleasePath', update?.releasePath || update?.releaseUrl || '\u2014');
  setText('systemUpdatePackagePath', update?.packageUrl || '\u2014');
  const notes = $('systemUpdateNotes');
  if (notes) {
    const values = Array.isArray(update?.notes) ? update.notes.filter(Boolean) : [];
    notes.innerHTML = values.length ? values.map(note => `<li>${escapeHtml(note)}</li>`).join('') : '<li>No release notes were provided by the stable feed.</li>';
  }
  const link = $('systemUpdateLink');
  if (link) {
    const url = update?.releaseUrl || update?.releasePath || '';
    link.classList.toggle('hidden', !url);
    if (url) link.href = url;
  }
  const packageLink = $('systemUpdatePackageLink');
  if (packageLink) {
    const url = update?.packageUrl || '';
    packageLink.classList.toggle('hidden', !url);
    if (url) packageLink.href = url;
  }
}

function notifyStableUpdateAvailable(update) {
  if (!update?.updateAvailable) return;
  const version = String(update.latestVersion || 'stable').trim() || 'stable';
  const key = `${version}|${update.releaseUrl || update.releasePath || ''}`;
  try {
    if (localStorage.getItem(GUIDEVAULT_UPDATE_NOTIFIED_VERSION_KEY) === key) return;
    localStorage.setItem(GUIDEVAULT_UPDATE_NOTIFIED_VERSION_KEY, key);
  } catch {}

  showStableUpdateToast(update);

  try {
    if ('Notification' in window && Notification.permission === 'granted') {
      const notification = new Notification(`Guidevault ${version} is available`, {
        body: 'A new stable Guidevault release has been published.',
        tag: `guidevault-update-${version}`
      });
      notification.onclick = () => {
        window.focus();
        const url = update.releaseUrl || update.releasePath || '';
        if (url) window.open(url, '_blank', 'noopener,noreferrer');
      };
    }
  } catch {}
}

function showStableUpdateToast(update) {
  let toast = $('guidevaultUpdateToast');
  if (!toast) {
    toast = document.createElement('div');
    toast.id = 'guidevaultUpdateToast';
    toast.className = 'guidevault-update-toast hidden';
    toast.setAttribute('role', 'status');
    toast.setAttribute('aria-live', 'polite');
    document.body.appendChild(toast);
  }
  const version = escapeHtml(update?.latestVersion || 'stable');
  const releaseUrl = update?.releaseUrl || update?.releasePath || '';
  toast.innerHTML = `
    <div class="guidevault-update-toast-icon">\u21A5</div>
    <div class="guidevault-update-toast-copy">
      <strong>Guidevault ${version} is available</strong>
      <span>A new stable release has been published.</span>
    </div>
    <div class="guidevault-update-toast-actions">
      ${releaseUrl ? '<button type="button" data-update-toast-action="open">Open release</button>' : ''}
    </div>`;
  toast.classList.remove('hidden');

  toast.querySelector('[data-update-toast-action="open"]')?.addEventListener('click', () => {
    if (releaseUrl) window.open(releaseUrl, '_blank', 'noopener,noreferrer');
    toast.classList.add('hidden');
  });

  if (state.updateToastTimer) window.clearTimeout(state.updateToastTimer);
  state.updateToastTimer = window.setTimeout(() => toast.classList.add('hidden'), 5500);
}



function normalizeStableVersion(value = '') {
  const match = String(value || '').match(/\d+(?:\.\d+){0,3}/);
  return match ? match[0] : String(value || '').replace(/^v/i, '').trim();
}

function compareStableVersions(latest = '', current = '') {
  const a = normalizeStableVersion(latest).split('.').map(n => Number(n) || 0);
  const b = normalizeStableVersion(current).split('.').map(n => Number(n) || 0);
  const len = Math.max(a.length, b.length, 3);
  for (let i = 0; i < len; i++) {
    const av = a[i] || 0;
    const bv = b[i] || 0;
    if (av > bv) return 1;
    if (av < bv) return -1;
  }
  return 0;
}

async function fetchStableUpdateDirectly(force = false, previousError = null) {
  const res = await fetch(GUIDEVAULT_STABLE_TAG_FEED_URL, { cache: 'no-store' });
  if (!res.ok) throw new Error(`GitHub tag feed returned HTTP ${res.status}`);
  const tags = await res.json();
  const latest = Array.isArray(tags) && tags.length ? tags[0] : null;
  const tagName = latest?.name || latest?.tag_name || '';
  const latestVersion = normalizeStableVersion(tagName) || GUIDEVAULT_APP_VERSION;
  const releaseUrl = tagName ? `${GUIDEVAULT_RELEASES_URL}/tag/${encodeURIComponent(tagName)}` : GUIDEVAULT_RELEASES_URL;
  const updateAvailable = compareStableVersions(latestVersion, GUIDEVAULT_APP_VERSION) > 0;

  return {
    configured: true,
    channel: 'stable',
    currentVersion: GUIDEVAULT_APP_VERSION,
    currentImage: GUIDEVAULT_CURRENT_IMAGE,
    latestVersion,
    latestImage: GUIDEVAULT_CURRENT_IMAGE,
    feedUrl: GUIDEVAULT_STABLE_TAG_FEED_URL,
    releaseUrl,
    releasePath: releaseUrl,
    packageUrl: GUIDEVAULT_PACKAGE_URL,
    notes: [],
    forced: force,
    checkedAt: Date.now(),
    updateAvailable,
    status: updateAvailable ? 'available' : 'current',
    message: updateAvailable
      ? `Guidevault ${latestVersion} is available on the stable channel.`
      : `Guidevault is current. Checked GitHub tags directly${previousError ? ' after the backend check failed' : ''}.`
  };
}

async function checkStableUpdates(force = false) {
  if (!force && state.updateCheck?.checkedAt && Date.now() - state.updateCheck.checkedAt < GUIDEVAULT_UPDATE_CHECK_MS) {
    renderUpdateNotification();
    return state.updateCheck;
  }
  try {
    const res = await fetch(`/api/system/update-check${force ? '?force=true' : ''}`, { cache: 'no-store' });
    if (!res.ok) throw new Error(await res.text());
    state.updateCheck = await res.json();
    state.updateCheck.checkedAt = Date.now();
    renderUpdateNotification();
    notifyStableUpdateAvailable(state.updateCheck);
    return state.updateCheck;
  } catch (err) {
    console.warn('Stable update check failed through backend; trying direct GitHub tag feed.', err);
    try {
      state.updateCheck = await fetchStableUpdateDirectly(force, err);
    } catch (fallbackErr) {
      console.warn('Direct stable update check failed', fallbackErr);
      state.updateCheck = {
        configured: true,
        channel: 'stable',
        status: 'unverified',
        message: `Could not verify stable updates right now. ${fallbackErr?.message || err?.message || ''}`.trim(),
        currentVersion: GUIDEVAULT_APP_VERSION,
        latestVersion: GUIDEVAULT_APP_VERSION,
        currentImage: GUIDEVAULT_CURRENT_IMAGE,
        latestImage: GUIDEVAULT_CURRENT_IMAGE,
        feedUrl: GUIDEVAULT_STABLE_TAG_FEED_URL,
        releaseUrl: GUIDEVAULT_RELEASES_URL,
        releasePath: GUIDEVAULT_RELEASES_URL,
        packageUrl: GUIDEVAULT_PACKAGE_URL,
        notes: [],
        updateAvailable: false,
        checkedAt: Date.now()
      };
    }
    renderUpdateNotification();
    notifyStableUpdateAvailable(state.updateCheck);
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
  return '\u2022'.repeat(length);
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
          <td><span class="opds-masked-key">${maskOpdsKey(key.secret)}</span><button class="opds-inline-copy" type="button" data-opds-action="copy-key" title="Copy key">\u29C9</button></td>
          <td>${escapeHtml(key.expiresAt ? formatOpdsDate(key.expiresAt) : 'Never')}</td>
          <td>${escapeHtml(formatOpdsDate(key.lastAccessed))}</td>
          <td class="opds-actions-cell"><button class="opds-action-button opds-rotate-key" type="button" data-opds-action="rotate" title="Rotate key">\u27F3</button><button class="opds-action-button danger" type="button" data-opds-action="delete" title="Delete key" aria-label="Delete key">${deviceIcon('trash')}</button></td>
        </tr>`).join('')
      : '<tr><td colspan="5" class="opds-empty-row">No authorization keys yet. Select + New to generate one.</td></tr>';
  }
  if ($('opdsKeyCount')) $('opdsKeyCount').textContent = `${settings.keys.length} total`;
}


function setServerSettingsStatus(message = '', tone = '') {
  const el = $('serverSettingsStatus');
  if (el) { el.textContent = message || ''; el.dataset.tone = tone || ''; }
}
function setIntegrationsSettingsStatus(message = '', tone = '') {
  const el = $('integrationsSettingsStatus');
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
  return { hostName: window.location?.origin || 'http://localhost:5478', baseUrl: '/', ipAddresses: '', port: 5478, loggingLevel: 'Information', backupDirectory: 'data/backups', bookmarksDirectory: 'data/bookmarks', igdbClientId: '', igdbClientSecret: '' };
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
    bookmarksDirectory: String(value.bookmarksDirectory || defaults.bookmarksDirectory).trim(),
    igdbClientId: String(value.igdbClientId || '').trim(),
    igdbClientSecret: String(value.igdbClientSecret || '').trim()
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
  if ($('igdbClientId')) $('igdbClientId').value = settings.igdbClientId || '';
  if ($('igdbClientSecret')) $('igdbClientSecret').value = settings.igdbClientSecret || '';
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
    bookmarksDirectory: $('mediaBookmarksDirectory')?.value ?? existing.bookmarksDirectory,
    igdbClientId: $('igdbClientId')?.value ?? existing.igdbClientId,
    igdbClientSecret: $('igdbClientSecret')?.value ?? existing.igdbClientSecret
  });
}
async function saveServerSettings(source = 'general') {
  const payload = collectServerSettings();
  try {
    const res = await fetch('/api/server/settings', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
    if (!res.ok) throw new Error(`Save failed: ${res.status}`);
    state.serverSettings = normalizeServerSettings(await res.json());
    renderServerSettings();
    const msg = source === 'media' ? 'Media settings saved.' : (source === 'integrations' ? 'Integrations saved.' : 'General server settings saved. Restart Guidevault if you changed listener values.');
    if (source === 'media') setMediaSettingsStatus(msg, 'success');
    else if (source === 'integrations') setIntegrationsSettingsStatus(msg, 'success');
    else setServerSettingsStatus(msg, 'success');
  } catch (err) {
    console.error('Unable to save server settings', err);
    const msg = `Unable to save settings: ${err?.message || err}`;
    if (source === 'media') setMediaSettingsStatus(msg, 'error');
    else if (source === 'integrations') setIntegrationsSettingsStatus(msg, 'error');
    else setServerSettingsStatus(msg, 'error');
  }
}
async function testIgdbCredentials() {
  const payload = collectServerSettings();
  const setIgdbStatus = $('integrationsSettingsStatus') ? setIntegrationsSettingsStatus : setServerSettingsStatus;
  setIgdbStatus('Saving and testing IGDB credentials...', 'info');
  try {
    const saveRes = await fetch('/api/server/settings', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
    if (!saveRes.ok) throw new Error(`Save failed: ${saveRes.status}`);
    state.serverSettings = normalizeServerSettings(await saveRes.json());
    renderServerSettings();
    const data = await metadataBatchFetchJson('/api/igdb/status');
    setIgdbStatus(data?.message || 'IGDB credentials verified.', 'success');
  } catch (err) {
    console.error('IGDB credential test failed', err);
    setIgdbStatus(`IGDB credential test failed: ${metadataBatchFriendlySourceError('igdb', err?.message || err)}`, 'error');
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
      <div class="email-history-main"><strong>${escapeHtml(item.subject || 'Guidevault email')}</strong><span>${escapeHtml(item.type || 'Email')} \u2022 ${escapeHtml(item.to || '\u2014')}</span><small>${escapeHtml(sentAt)}</small></div>
      <div class="email-history-meta"><span>${escapeHtml(status)}</span><em>${escapeHtml(item.templateName || 'Guidevault Invite')}</em></div>
      ${item.message ? `<p class="sub email-history-message">${escapeHtml(item.message)}</p>` : ''}
    </article>`;
  }).join('') : '<article class="settings-card"><p class="sub">No email has been sent or attempted yet.</p></article>';
}

async function loadSystemEvents(showStatus = false) {
  try {
    const res = await fetch('/api/system/events?limit=100', { cache: 'no-store' });
    if (!res.ok) throw new Error(`System events request failed: ${res.status}`);
    const data = await res.json();
    state.systemEvents = Array.isArray(data) ? data : (Array.isArray(data.events) ? data.events : []);
    renderSystemEvents();
    if (showStatus) setSystemEventsStatus('System events refreshed.', 'success');
  } catch (err) {
    console.warn('Unable to load system events', err);
    state.systemEvents = Array.isArray(state.systemEvents) ? state.systemEvents : [];
    renderSystemEvents();
    if (showStatus) setSystemEventsStatus('Unable to load system events from the backend.', 'error');
  }
}
function setSystemEventsStatus(message = '', tone = '') {
  const el = $('systemEventsStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}
function systemEventIcon(category = '') {
  const key = String(category || '').toLowerCase();
  if (key.includes('reader')) return '&#x1F4D6;';
  if (key.includes('metadata')) return '&#x270E;';
  if (key.includes('connection') || key.includes('device')) return '&#x1F517;';
  if (key.includes('email')) return '&#x2709;';
  if (key.includes('file')) return '&#x1F5C2;';
  if (key.includes('library')) return '&#x1F4DA;';
  if (key.includes('api')) return '&#x1F5A7;';
  return '&#x25C8;';
}
function renderSystemEvents() {
  const host = $('systemEventsList');
  if (!host) return;
  const items = Array.isArray(state.systemEvents) ? state.systemEvents.slice(0, 100) : [];
  if ($('systemEventsCount')) $('systemEventsCount').textContent = String(items.length);
  if ($('systemEventsLast')) {
    const last = items[0]?.createdAt ? new Date(items[0].createdAt).toLocaleString() : '\u2014';
    $('systemEventsLast').textContent = last;
  }
  host.innerHTML = items.length ? items.map(item => {
    const created = item.createdAt ? new Date(item.createdAt).toLocaleString() : 'Unknown time';
    const category = item.category || 'System';
    const title = item.title || item.message || 'Guidevault event';
    const source = item.source || 'server';
    return `<article class="settings-card system-event-row" data-category="${escapeForAttribute(String(category).toLowerCase())}">
      <div class="system-event-icon" aria-hidden="true">${systemEventIcon(category)}</div>
      <div class="system-event-main">
        <div class="system-event-title-line"><strong>${escapeHtml(title)}</strong><span>${escapeHtml(category)}</span></div>
        ${item.message ? `<p>${escapeHtml(item.message)}</p>` : ''}
        <div class="system-event-foot"><span>${escapeHtml(created)}</span><span>${escapeHtml(source)}</span>${item.itemTitle ? `<span>${escapeHtml(item.itemTitle)}</span>` : ''}</div>
      </div>
    </article>`;
  }).join('') : '<article class="settings-card"><p class="sub">No system events have been recorded yet.</p></article>';
}
function usersSettingsRuntime() {
  if (!state.usersSettingsRuntime) state.usersSettingsRuntime = { loaded: false, loading: false, requestId: 0, loadTimer: 0, renderedHash: '' };
  return state.usersSettingsRuntime;
}
function deferAfterVisiblePaint(callback, delayMs = 0) {
  const run = () => {
    if (delayMs > 0) window.setTimeout(callback, delayMs);
    else callback();
  };
  if (typeof requestAnimationFrame === 'function') {
    requestAnimationFrame(() => requestAnimationFrame(run));
  } else {
    window.setTimeout(run, delayMs || 0);
  }
}
function renderUsersLoadingState() {
  const usersHost = $('usersList');
  if (usersHost && !usersHost.dataset.loadingRendered) {
    usersHost.dataset.loadingRendered = '1';
    usersHost.innerHTML = '<article class="settings-card user-card refined-user-card users-loading-card"><div class="user-avatar-badge generic">GV</div><div class="user-card-main"><h3>Loading users...</h3><p class="sub">Opening the Users panel now. Account details will refresh in the background.</p></div></article>';
  }
  const libHost = $('inviteLibrariesList');
  if (libHost && !libHost.dataset.loadingRendered) {
    libHost.dataset.loadingRendered = '1';
    libHost.innerHTML = '<p class="sub users-inline-loading">Loading library access...</p>';
  }
  const permHost = $('invitePermissionsList');
  if (permHost && !permHost.dataset.loadingRendered) {
    permHost.dataset.loadingRendered = '1';
    permHost.innerHTML = '<p class="sub users-inline-loading">Loading permissions...</p>';
  }
}
function openUsersSettingsPanel() {
  const runtime = usersSettingsRuntime();
  const hasCachedData = runtime.loaded || !!((state.usersSettings?.users || []).length || (state.usersSettings?.libraries || []).length || (state.usersSettings?.permissions || []).length);
  if (hasCachedData) {
    renderUsersSettings({ allowSkip: true });
    return;
  }
  renderUsersLoadingState();
  scheduleUsersSettingsLoad(false, 90);
}
function scheduleUsersSettingsLoad(showStatus = false, delayMs = 0) {
  const runtime = usersSettingsRuntime();
  if (runtime.loadTimer) {
    window.clearTimeout(runtime.loadTimer);
    runtime.loadTimer = 0;
  }
  deferAfterVisiblePaint(() => {
    runtime.loadTimer = window.setTimeout(() => {
      runtime.loadTimer = 0;
      loadUsersSettings(showStatus);
    }, Math.max(0, delayMs));
  });
}
async function loadUsersSettings(showStatus = false) {
  const runtime = usersSettingsRuntime();
  if (runtime.loading) return;
  runtime.loading = true;
  const requestId = ++runtime.requestId;
  try {
    const res = await fetch('/api/users', { cache: 'no-store' });
    if (!res.ok) throw new Error(`Users request failed: ${res.status}`);
    const data = await res.json();
    if (requestId !== runtime.requestId) return;
    state.usersSettings = { users: Array.isArray(data.users) ? data.users : [], libraries: Array.isArray(data.libraries) ? data.libraries : [], permissions: Array.isArray(data.permissions) ? data.permissions : [] };
    runtime.loaded = true;
    runtime.loading = false;
    deferAfterVisiblePaint(() => renderUsersSettings(), 0);
    if (showStatus) setUsersStatus('Users refreshed.', 'success');
  } catch (err) {
    runtime.loading = false;
    console.warn('Unable to load users', err);
    deferAfterVisiblePaint(() => renderUsersSettings(), 0);
    if (showStatus) setUsersStatus('Unable to load users from the backend.', 'error');
  }
}
function libraryNameForInvite(lib, index = 0) {
  return String(lib?.name || lib?.Name || lib?.libraryPath || lib?.LibraryPath || lib?.path || lib?.Path || `Library ${index + 1}`).trim();
}
function renderUsersSettings(options = {}) {
  const data = state.usersSettings || { users: [], libraries: [], permissions: [] };
  const runtime = usersSettingsRuntime();
  const users = Array.isArray(data.users) ? data.users : [];
  const libraries = data.libraries?.length ? data.libraries : (state.libraries || []);
  const permissions = data.permissions?.length ? data.permissions : ['Login', 'Bookmark', 'Download', 'Read Only'];
  const renderHash = JSON.stringify({
    users: users.map(user => [user.email, user.displayName, user.role, user.status, user.ageRatingRestriction, user.libraries, user.permissions]),
    libraries,
    permissions
  });
  if (options.allowSkip && runtime.renderedHash === renderHash) return;
  runtime.renderedHash = renderHash;

  const libHost = $('inviteLibrariesList');
  if (libHost) {
    delete libHost.dataset.loadingRendered;
    libHost.innerHTML = libraries.map((lib, index) => {
      const name = libraryNameForInvite(lib, index);
      return `<label class="inline-check"><input type="checkbox" value="${escapeForAttribute(name)}" checked /> <span>${escapeHtml(name)}</span></label>`;
    }).join('') || '<p class="sub">No libraries configured yet.</p>';
  }
  const permHost = $('invitePermissionsList');
  if (permHost) {
    delete permHost.dataset.loadingRendered;
    permHost.innerHTML = permissions.map(name => `<label class="inline-check"><input type="checkbox" value="${escapeForAttribute(name)}" ${['Login','Bookmark','Read Only'].includes(name) ? 'checked' : ''} /> <span>${escapeHtml(name)}</span></label>`).join('');
  }
  const usersHost = $('usersList');
  if (usersHost) {
    delete usersHost.dataset.loadingRendered;
    usersHost.innerHTML = users.map(user => {
      const initials = String(user.displayName || user.email || 'GV').trim().split(/\s+/).slice(0,2).map(x => x[0] || '').join('').toUpperCase() || 'GV';
      const userLibraries = (user.libraries || []).join(', ') || 'No library access';
      const userPermissions = (user.permissions || []).join(', ') || 'No permissions';
      return `<article class="settings-card user-card refined-user-card">
        <div class="user-avatar-badge">${escapeHtml(initials)}</div>
        <div class="user-card-main"><h3>${escapeHtml(user.displayName || user.email || 'Invited user')}</h3><p class="sub">${escapeHtml(user.email || '')}</p><div class="user-chip-list left"><span>${escapeHtml(user.role || 'Reader')}</span><span>${escapeHtml(user.status || 'Invited')}</span><span>${escapeHtml(user.ageRatingRestriction || 'No Restriction')}</span></div></div>
        <div class="user-access-summary"><strong>Libraries</strong><span>${escapeHtml(userLibraries)}</span><strong>Permissions</strong><span>${escapeHtml(userPermissions)}</span></div>
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
  return width && height ? `${width}\u00D7${height}${orientation ? ` (${orientation})` : ''}` : '';
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

function formatDeviceDate(value, fallback = '\u2014') {
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

function normalizeUiText(value = '') {
  return String(value ?? '')
    .replace(/\u00C2\u2014/g, '\u00D7')
    .replace(/\u00C3\u2014/g, '\u00D7')
    .replace(/\u00E2\u20AC\u201D/g, '\u2014')
    .replace(/\u00E2\u20AC\u201C/g, '\u2013')
    .replace(/\u00E2\u20AC\u2122/g, "'")
    .replace(/\u00E2\u20AC\u0153/g, '"')
    .replace(/\u00E2\u20AC\u009D/g, '"')
    .replace(/\u00E2\u20AC\u00A2/g, '\u2022')
    .replace(/\s+/g, ' ')
    .trim();
}

function normalizeDeviceScreen(value = '') {
  const text = normalizeUiText(value || '\u2014');
  return text
    .replace(/(\d+)\s*(?:\u00D7|x|\u2014|\u2013|-)\s*(\d+)/gi, '$1\u00D7$2')
    .replace(/\s+/g, ' ')
    .trim();
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
    trash: '<path d="M3 6h18"></path><path d="M8 6V4h8v2"></path><path d="M19 6l-1 14H6L5 6"></path><path d="M10 11v6"></path><path d="M14 11v6"></path>',
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
  const displayValue = iconName === 'screen'
    ? normalizeDeviceScreen(value || '\u2014')
    : normalizeUiText(value || '\u2014');
  return `<div class="device-card-fact">${deviceIcon(iconName)}<span>${escapeHtml(label)}: <b>${escapeHtml(displayValue || '\u2014')}</b></span></div>`;
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
          <td><span class="device-email-name-cell">${deviceIcon('opds')}<strong>${escapeHtml(device.name || 'Email Device')}</strong></span></td>
          <td><span class="device-email-address">${escapeHtml(device.email || '\u2014')}</span></td>
          <td><span class="device-badge web">${escapeHtml(device.platform || 'Email')}</span></td>
          <td><button class="device-table-action danger" type="button" data-device-email-action="delete" title="Delete device" aria-label="Delete device">${deviceIcon('trash')}</button></td>
        </tr>`).join('')
      : '<tr><td colspan="4" class="device-empty-row">No data to display</td></tr>';
  }
  if ($('deviceEmailCount')) $('deviceEmailCount').textContent = `${emailDevices.length} total`;
}

function clientDeviceBadges(device) {
  const badges = [];
  if (device.isActive) badges.push('<span class="device-badge active">Active</span>');
  if (device.clientType) {
    const typeText = normalizeUiText(device.clientType);
    badges.push(`<span class="device-badge ${String(device.clientType).toLowerCase().includes('opds') ? 'opds' : 'web'}">${escapeHtml(typeText)}</span>`);
  }
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
    const browserText = device.browserName
      ? normalizeUiText(`${device.browserName}${browserVersion ? ` ${browserVersion}` : ''}`)
      : '\u2014';
    const userText = normalizeUiText(device.username || device.authKeyName || device.email || 'local');
    const displayName = normalizeUiText(device.displayName || 'Guidevault Client');
    const platformText = normalizeUiText(device.platform || 'Unknown');
    const screenText = normalizeDeviceScreen(device.screen || '\u2014');
    const appVersionText = normalizeUiText(device.appVersion || '\u2014');
    return `
      <article class="device-client-card" data-client-device-id="${escapeForAttribute(id)}">
        ${manageable && !isEditing ? `
          <div class="device-card-menu">
            <button class="device-card-menu-button" type="button" data-device-client-action="toggle-menu" title="Device options" aria-label="Device options">\u22EE</button>
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
          ${deviceFact('platform', 'Platform', platformText)}
          ${deviceFact('screen', 'Screen', screenText)}
          ${deviceFact('version', 'App Version', appVersionText)}
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
  const match = screen.match(/(\d+)\s*[\u00D7x]\s*(\d+)/i);
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
    $('rightToggleTop').textContent = collapsed ? '\u203A' : '\u2039';
    $('rightToggleTop').title = collapsed ? 'Show details panel' : 'Hide details panel';
    $('rightToggleTop').setAttribute('aria-label', $('rightToggleTop').title);
  }
  if ($('rightToggle')) {
    $('rightToggle').textContent = collapsed ? '\u2039' : '\u203A';
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
  if (state.activeTab === 'library-data') { updateMetadataFileMaintenance(); }
  if (state.activeTab === 'reviews' && state.selected) loadPublicReviewsForItem(state.selected, true);
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
    iconKey(raw.replace(/\bplaystation\s*2\b/i, 'Sony Playstation 2')),
    iconKey(raw.replace(/\bplaystation\s*3\b/i, 'Sony Playstation 3')),
    iconKey(raw.replace(/\bplaystation\s*4\b/i, 'Sony Playstation 4')),
    iconKey(raw.replace(/\bplaystation\s*5\b/i, 'Sony Playstation 5')),
    iconKey(raw.replace(/\bplaystation\s*portable\b/i, 'Sony PSP')),
    iconKey(raw.replace(/^xbox$/i, 'Microsoft Xbox')),
    iconKey(raw.replace(/^ms[-\s]?dos$/i, 'DOS')),
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
  if (!state.libraries.length && state.libraryPath) state.libraries = [{ name: 'Manuals', type: 'Manuals', folders: [state.libraryPath], lastScanned: null }];
  if ($('libraryRootText')) $('libraryRootText').textContent = state.libraryPath || 'Not set';
  if ($('libraryPathInput')) $('libraryPathInput').value = state.libraryPath || '';
  renderLibrariesSettings();
}


function normalizeLibraryPayload(data) {
  return Array.isArray(data) ? data : (Array.isArray(data?.items) ? data.items : []);
}

function normalizeLibrarySummary(data) {
  if (!data || Array.isArray(data)) return null;
  const counts = data.counts || {};
  const total = Number(data.totalCount ?? data.total ?? counts.all ?? 0) || 0;
  return {
    totalCount: total,
    counts: {
      Manual: Number(counts.manual ?? counts.Manual ?? 0) || 0,
      'Strategy Guide': Number(counts.strategyGuide ?? counts.StrategyGuide ?? counts['Strategy Guide'] ?? 0) || 0,
      Magazine: Number(counts.magazine ?? counts.Magazine ?? 0) || 0
    },
    message: String(data.message || ''),
    generatedAt: String(data.generatedAt || '')
  };
}

function isUnfilteredAllContentView() {
  const q = ($('search')?.value || '').trim();
  return !q && !state.customFilter && !state.categoryFilter && state.filter === 'All Content';
}

function currentLibraryTotalCount() {
  if (state.libraryIsPartial && state.librarySummary?.totalCount && isUnfilteredAllContentView()) return state.librarySummary.totalCount;
  return Array.isArray(state.filtered) ? state.filtered.length : 0;
}

function scheduleGuidevaultIdleWork(fn) {
  if (typeof requestIdleCallback === 'function') return requestIdleCallback(fn, { timeout: 700 });
  return window.setTimeout(fn, 60);
}

function guidevaultYieldToUi(timeout = GUIDEVAULT_LIBRARY_CHUNK_YIELD_MS) {
  return new Promise(resolve => {
    if (typeof requestIdleCallback === 'function') {
      requestIdleCallback(() => resolve(), { timeout: Math.max(20, timeout) });
    } else {
      window.setTimeout(resolve, Math.max(0, timeout));
    }
  });
}

function libraryItemRenderFingerprint(item) {
  if (!item) return '';
  return [
    item.id || item.Id || '',
    item.modified || item.Modified || '',
    item.sizeBytes || item.SizeBytes || '',
    item.title || item.name || '',
    item.kind || '',
    item.category || item.system || '',
    item.metadataStatus || '',
    item.publisher || '',
    item.year || '',
    item.languageTag || '',
    item.region || '',
    item.pageCount || item.metadataPageCount || ''
  ].map(value => String(value ?? '').trim()).join('\u00A6');
}

function libraryPayloadFingerprint(items = []) {
  const list = Array.isArray(items) ? items : [];
  const length = list.length;
  if (!length) return '0';

  // Large libraries were previously fingerprinted by walking every item and
  // building one huge string. That made first load and full-index refreshes feel
  // frozen. Sample stable positions instead; server-generated timestamps/counts
  // are included by callers when available.
  const sampleIndexes = [0, 1, 2, Math.floor(length / 2), length - 3, length - 2, length - 1]
    .filter(index => index >= 0 && index < length);
  const uniqueIndexes = [...new Set(sampleIndexes)];
  return `${length}::${uniqueIndexes.map(index => libraryItemRenderFingerprint(list[index])).join('\u00A7')}`;
}

function takeBestLibraryItems(source, limit, compare, predicate = null) {
  const best = [];
  const max = Math.max(0, Number(limit || 0));
  if (!max) return best;
  for (const item of source || []) {
    if (!item || (predicate && !predicate(item))) continue;
    if (best.length < max) {
      best.push(item);
      continue;
    }
    let worstIndex = 0;
    for (let i = 1; i < best.length; i++) {
      if (compare(best[i], best[worstIndex]) > 0) worstIndex = i;
    }
    if (compare(item, best[worstIndex]) < 0) best[worstIndex] = item;
  }
  return best.sort(compare);
}

function startupCacheItemsForLibrary(items = state.items) {
  const source = Array.isArray(items) ? items : [];
  if (source.length <= GUIDEVAULT_LIBRARY_STARTUP_CACHE_LIMIT) return source.slice();

  const selected = new Map();
  const add = candidates => {
    for (const item of candidates || []) {
      const id = itemIdOf(item);
      if (!id || selected.has(id)) continue;
      selected.set(id, item);
      if (selected.size >= GUIDEVAULT_LIBRARY_STARTUP_CACHE_LIMIT) return true;
    }
    return false;
  };
  const byRecent = (a, b) => (libraryItemComputed(b).recent - libraryItemComputed(a).recent) || compareTextForSort(displayTitle(a), displayTitle(b));
  const byTitle = (a, b) => compareTextForSort(displayTitle(a), displayTitle(b));
  const byIssue = (a, b) => compareItemsByIssueThenTitle(a, b);
  add(takeBestLibraryItems(source, 72, byRecent));
  add(takeBestLibraryItems(source, 56, byTitle, i => i.kind === 'Manual'));
  add(takeBestLibraryItems(source, 56, byTitle, i => i.kind === 'Strategy Guide'));
  add(takeBestLibraryItems(source, 56, byIssue, i => i.kind === 'Magazine'));
  add(source);
  return [...selected.values()].slice(0, GUIDEVAULT_LIBRARY_STARTUP_CACHE_LIMIT);
}

function saveLibraryClientCache(items = state.items) {
  try {
    const startupItems = startupCacheItemsForLibrary(items);
    localStorage.setItem(GUIDEVAULT_LIBRARY_CACHE_KEY, JSON.stringify({
      version: GUIDEVAULT_APP_VERSION,
      savedAt: new Date().toISOString(),
      isStartupSubset: true,
      totalCount: Array.isArray(items) ? items.length : startupItems.length,
      items: startupItems
    }));
  } catch (err) {
    console.debug('Guidevault library browser cache was not saved.', err);
  }
}

function loadLibraryClientCache() {
  try {
    const raw = localStorage.getItem(GUIDEVAULT_LIBRARY_CACHE_KEY);
    if (!raw) return [];
    if (raw.length > GUIDEVAULT_LIBRARY_CACHE_MAX_BYTES) {
      // Older builds saved the complete library index in synchronous localStorage.
      // Drop oversized caches instead of parsing a multi-MB JSON blob on login.
      localStorage.removeItem(GUIDEVAULT_LIBRARY_CACHE_KEY);
      return [];
    }
    const data = JSON.parse(raw);
    const list = Array.isArray(data?.items) ? data.items : [];
    return list.slice(0, GUIDEVAULT_LIBRARY_STARTUP_CACHE_LIMIT);
  } catch (err) {
    console.debug('Guidevault library browser cache was not readable.', err);
    return [];
  }
}

function getGuidevaultStartupDeepLink() {
  try {
    const params = new URLSearchParams(window.location.search || '');
    const readId = (params.get('read') || params.get('reader') || '').trim();
    const detailId = (params.get('detail') || params.get('item') || '').trim();
    if (readId) return { action: 'read', id: readId };
    if (detailId) return { action: 'detail', id: detailId };
  } catch {}
  return null;
}

function clearGuidevaultStartupDeepLinkUrl() {
  try {
    const url = new URL(window.location.href);
    ['read', 'reader', 'detail', 'item'].forEach(key => url.searchParams.delete(key));
    const clean = `${url.pathname}${url.search}${url.hash}`;
    history.replaceState(null, '', clean || '/');
  } catch {}
}

function handleGuidevaultStartupDeepLink() {
  if (guidevaultStartupDeepLinkHandled || !Array.isArray(state.items) || !state.items.length) return false;
  const link = getGuidevaultStartupDeepLink();
  if (!link?.id) return false;
  const wanted = String(link.id || '').trim();
  const wantedLower = wanted.toLowerCase();
  const item = state.items.find(i => itemIdOf(i).toLowerCase() === wantedLower);
  if (!item) return false;
  guidevaultStartupDeepLinkHandled = true;
  clearGuidevaultStartupDeepLinkUrl();
  if (link.action === 'read') {
    openReader(item);
  } else {
    showDetailScreen(item);
  }
  return true;
}

function renderCachedLibraryImmediately() {
  if (Array.isArray(state.items) && state.items.length) return false;
  const cached = loadLibraryClientCache();
  if (!cached.length) return false;
  state.items = cached;
  applyClientMetadataOverridesToLibrary();
  refreshLibraryDerivedState();
  state.libraryLoadedOnce = true;
  state.libraryLastRenderedFingerprint = libraryPayloadFingerprint(state.items);
  applyFilters();
  scheduleCategoryPreviewCoverPrewarm('', { immediate: true, includeSecondary: true });
  handleGuidevaultStartupDeepLink();
  setStatus('Loaded cached library. Refreshing latest library state in the background...');
  return true;
}

function shouldDeferFullLibraryRender() {
  const libraryVisible = $('libraryView') && !$('libraryView').classList.contains('hidden');
  return libraryVisible
    && state.viewMode === 'all'
    && state.filter === 'All Content'
    && isUnfilteredAllContentView()
    && !getGuidevaultStartupDeepLink()?.id
    && !state.selected
    && ($('detailView')?.classList.contains('hidden') ?? true)
    && ($('readerView')?.classList.contains('hidden') ?? true);
}

function activateDeferredFullLibrary() {
  const pending = state.deferredFullLibraryItems;
  if (!Array.isArray(pending) || !pending.length) return false;
  const selectedId = state.selected?.id || state.selected?.Id || '';
  state.deferredFullLibraryItems = null;
  state.items = pending;
  state.libraryIsPartial = false;
  state.librarySummary = { totalCount: pending.length, counts: null, message: '' };
  applyClientMetadataOverridesToLibrary();
  refreshLibraryDerivedState();
  if (selectedId) state.selected = state.items.find(i => String(i.id || i.Id || '') === String(selectedId)) || null;
  state.libraryLastRenderedFingerprint = libraryPayloadFingerprint(state.items) + `::partial=false::activated=${Date.now()}`;
  return true;
}

async function loadLibrary(options = {}) {
  const preferFastStartup = options.preferFastStartup !== false;
  try {
    const iconPromise = Object.keys(state.iconMap || {}).length ? Promise.resolve() : loadPlatformIcons();
    const settingsPromise = loadSettings().catch(err => { console.warn('Settings load failed', err); });
    const url = preferFastStartup
      ? `/api/library/initial?limit=240&_=${Date.now()}`
      : `/api/library?_=${Date.now()}`;
    const libraryPromise = fetch(url, { cache: 'no-store' });
    await Promise.all([iconPromise, settingsPromise]);
    const res = await libraryPromise;
    if (!res.ok) throw new Error(await res.text());
    const data = await res.json();
    const nextItems = normalizeLibraryPayload(data);
    const summary = normalizeLibrarySummary(data);
    const isPartial = !!data?.isPartial;

    state.deferredFullLibraryItems = null;
    state.items = nextItems;
    state.librarySummary = summary;
    state.libraryIsPartial = isPartial;
    applyClientMetadataOverridesToLibrary();
    refreshLibraryDerivedState();
    state.libraryLoadedOnce = true;
    if (!isPartial) saveLibraryClientCache(state.items);
    if (state.selected) state.selected = state.items.find(i => i.id === state.selected.id) || null;
    const nextFingerprint = libraryPayloadFingerprint(state.items) + `::partial=${isPartial}::generated=${summary?.generatedAt || data?.generatedAt || ''}`;
    const canSkipRender = state.libraryLastRenderedFingerprint && state.libraryLastRenderedFingerprint === nextFingerprint;
    if (!canSkipRender) {
      state.libraryLastRenderedFingerprint = nextFingerprint;
      applyFilters();
      scheduleCategoryPreviewCoverPrewarm('', { immediate: true, includeSecondary: true });
    } else {
      updateSettingsInsights();
      setStatus(isPartial ? (summary?.message || 'Fast library startup loaded. Full library is loading in the background...') : 'Library is up to date.');
    }
    handleGuidevaultStartupDeepLink();
    if (!$('settingsReadingProfilesPanel')?.classList.contains('hidden')) renderReadingProfileSettings();

    if (isPartial && preferFastStartup) {
      setStatus(summary?.message || 'Fast library startup loaded. Full library is loading in the background...');
      scheduleGuidevaultIdleWork(() => {
        window.setTimeout(() => loadFullLibraryInBackground(), GUIDEVAULT_LIBRARY_FULL_RENDER_DELAY_MS);
      });
    }
  } catch (err) {
    console.error(err);
    const hadItems = Array.isArray(state.items) && state.items.length > 0;
    if (!hadItems) {
      state.items = [];
      state.filtered = [];
      state.libraryIsPartial = false;
      state.librarySummary = null;
      render();
    }
    setStatus('Library failed to load. Check the terminal for scan errors; existing results were kept if available.');
    console.warn('Library failed to load. Check the terminal for scan errors.');
  }
}

function itemIdOfForMerge(item) {
  return String(item?.id || item?.Id || '').trim();
}

function mergeLibraryItemsById(existingItems = [], nextItems = []) {
  const map = new Map();
  (Array.isArray(existingItems) ? existingItems : []).forEach(item => {
    const id = itemIdOfForMerge(item);
    if (id) map.set(id, item);
  });
  (Array.isArray(nextItems) ? nextItems : []).forEach(item => {
    const id = itemIdOfForMerge(item);
    if (id) map.set(id, item);
  });
  return [...map.values()];
}

async function fetchLibraryChunk(offset = 0, limit = GUIDEVAULT_LIBRARY_CHUNK_SIZE) {
  const params = new URLSearchParams({
    offset: String(Math.max(0, Number(offset || 0))),
    limit: String(Math.max(60, Number(limit || GUIDEVAULT_LIBRARY_CHUNK_SIZE))),
    _: String(Date.now())
  });
  const res = await fetch(`/api/library/chunk?${params.toString()}`, { cache: 'no-store' });
  if (!res.ok) throw new Error(await res.text());
  const data = await res.json();
  return {
    data,
    items: normalizeLibraryPayload(data),
    summary: normalizeLibrarySummary(data),
    total: Number(data?.totalCount ?? data?.total ?? 0) || 0,
    offset: Number(data?.offset ?? offset) || 0,
    nextOffset: Number(data?.nextOffset ?? (Number(offset || 0) + (Array.isArray(data?.items) ? data.items.length : 0))) || 0,
    hasMore: !!data?.hasMore
  };
}

async function loadFullLibraryInBackground() {
  if (state.libraryFullLoadPromise) return state.libraryFullLoadPromise;
  state.libraryFullLoadPromise = (async () => {
    const initialItems = Array.isArray(state.items) ? state.items.slice() : [];
    let mergedItems = initialItems;
    let offset = 0;
    let loaded = 0;
    let total = Number(state.librarySummary?.totalCount || 0) || 0;
    let counts = state.librarySummary?.counts || null;
    let generatedAt = '';
    state.libraryBackgroundLoad = { running: true, loaded: 0, total };

    try {
      while (true) {
        await guidevaultYieldToUi();
        const chunk = await fetchLibraryChunk(offset, GUIDEVAULT_LIBRARY_CHUNK_SIZE);
        if (chunk.summary?.counts && Object.values(chunk.summary.counts).some(v => Number(v || 0) > 0)) counts = chunk.summary.counts;
        if (chunk.summary?.generatedAt) generatedAt = chunk.summary.generatedAt;
        if (chunk.total) total = chunk.total;
        mergedItems = mergeLibraryItemsById(mergedItems, chunk.items);
        loaded = Math.max(chunk.nextOffset, loaded + chunk.items.length);
        state.libraryBackgroundLoad = { running: chunk.hasMore, loaded: Math.min(loaded, total || loaded), total: total || loaded };
        if (total) {
          state.librarySummary = {
            ...(state.librarySummary || {}),
            totalCount: total,
            counts: counts || state.librarySummary?.counts || null,
            generatedAt: generatedAt || state.librarySummary?.generatedAt || ''
          };
          state.libraryIsPartial = chunk.hasMore || mergedItems.length < total;
          setStatus(chunk.hasMore
            ? `Loading library index in the background... ${Math.min(loaded, total).toLocaleString()} of ${total.toLocaleString()} items.`
            : `Library index downloaded. Preparing ${total.toLocaleString()} items...`);
        }
        if (!chunk.hasMore || !chunk.items.length) break;
        offset = chunk.nextOffset;
      }

      await guidevaultYieldToUi(80);
      const currentSelectedId = state.selected?.id || state.selected?.Id || '';
      state.deferredFullLibraryItems = null;
      state.items = applyClientMetadataOverridesToLibrary(mergedItems);
      state.libraryIsPartial = false;
      state.librarySummary = {
        totalCount: state.items.length,
        counts: counts || state.librarySummary?.counts || null,
        message: '',
        generatedAt: generatedAt || new Date().toISOString()
      };
      refreshLibraryDerivedState();
      saveLibraryClientCache(state.items);
      if (currentSelectedId) state.selected = state.items.find(i => itemIdOfForMerge(i) === String(currentSelectedId)) || null;
      state.libraryLastRenderedFingerprint = libraryPayloadFingerprint(state.items) + `::partial=false::chunked=${state.librarySummary.generatedAt}`;

      await guidevaultYieldToUi(80);
      applyFilters();
      scheduleCategoryPreviewCoverPrewarm('', { immediate: true, includeSecondary: true });
      handleGuidevaultStartupDeepLink();
      if (!$('settingsReadingProfilesPanel')?.classList.contains('hidden')) renderReadingProfileSettings();
      setStatus('Full library loaded.');
    } catch (err) {
      console.warn('Full library background load failed.', err);
      setStatus('Fast library startup loaded, but the full background refresh failed. Existing results were kept.');
    } finally {
      state.libraryBackgroundLoad = { running: false, loaded: state.libraryBackgroundLoad?.loaded || 0, total: state.libraryBackgroundLoad?.total || 0 };
      state.libraryFullLoadPromise = null;
    }
  })();
  return state.libraryFullLoadPromise;
}

async function refreshStartupStatusOnce() {
  try {
    const res = await fetch(`/api/startup/status?_=${Date.now()}`, { cache: 'no-store' });
    if (!res.ok) return;
    const data = await res.json();
    const running = Array.isArray(data?.runningTasks) ? data.runningTasks[0] : null;
    if (running?.message) {
      setStatus(running.message);
    } else if (Number(data?.itemCount || 0) > 0) {
      setStatus(`Preparing library from ${Number(data.itemCount).toLocaleString()} indexed item(s)...`);
    }
  } catch {}
}

function setStatus(message = '') {
  const text = String(message || '');
  if ($('libraryDialogStatus')) $('libraryDialogStatus').textContent = text;
  if ($('settingsLibraryStatus')) $('settingsLibraryStatus').textContent = text;

  const startupStatus = $('libraryStartupStatus');
  if (!startupStatus) return;
  window.clearTimeout(guidevaultStartupStatusHideTimer);
  startupStatus.textContent = text;
  startupStatus.dataset.tone = /fail|error/i.test(text) ? 'error' : /loaded|ready|up to date/i.test(text) ? 'success' : 'info';
  startupStatus.classList.toggle('hidden', !text);
  if (/^Full library loaded\.?$|^Library is up to date\.?$/i.test(text)) {
    guidevaultStartupStatusHideTimer = window.setTimeout(() => {
      if (startupStatus.textContent === text) startupStatus.classList.add('hidden');
    }, GUIDEVAULT_STARTUP_STATUS_HIDE_MS);
  }
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
    type: lib?.type || lib?.Type || 'Manuals',
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
      <td>${escapeHtml(lib.type || 'Manuals')}</td>
      <td><div class="library-folder-path">${folder ? escapeHtml(folder) : '<span class="sub">No folder set</span>'}</div></td>
      <td class="library-actions">
        <button class="small-icon rescan-library" data-index="${index}" title="Rescan this library">\u27F3</button>
        <button class="small-icon edit-library" data-index="${index}" title="Edit library">\u270E</button>
        <button class="small-icon danger remove-library" data-index="${index}" title="Remove library" aria-label="Remove library">${deviceIcon('trash')}</button>
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
  const lib = state.editingLibraryIndex === null ? { name: '', type: 'Manuals', folders: [] } : state.libraries[state.editingLibraryIndex];
  if (!$('libraryEditor')) return;
  $('libraryEditor').classList.remove('hidden');
  $('libraryEditorTitle').textContent = state.editingLibraryIndex === null ? 'Add Library' : 'Edit Library';
  $('libraryNameInput').value = lib?.name || '';
  $('libraryTypeInput').value = lib?.type || 'Manuals';
  $('libraryFolderInput').value = (lib?.folders || [])[0] || '';
  $('libraryNameInput').focus();
}

function closeLibraryEditor() {
  state.editingLibraryIndex = null;
  if ($('libraryEditor')) $('libraryEditor').classList.add('hidden');
}

function normalizeLibraryFolderForCompare(folder) {
  return String(folder || '')
    .trim()
    .replace(/\\/g, '/')
    .replace(/\/+$/g, '')
    .toLowerCase();
}

function findExistingLibraryFolderIndex(folder, exceptIndex = null) {
  const key = normalizeLibraryFolderForCompare(folder);
  if (!key) return -1;
  return (state.libraries || []).findIndex((lib, index) => {
    if (exceptIndex !== null && index === exceptIndex) return false;
    const folders = Array.isArray(lib?.folders) ? lib.folders : (lib?.folder ? [lib.folder] : []);
    return folders.some(existing => normalizeLibraryFolderForCompare(existing) === key);
  });
}

async function saveLibraryEditor() {
  const name = $('libraryNameInput')?.value.trim() || 'Library';
  const type = $('libraryTypeInput')?.value || 'Manuals';
  const folder = $('libraryFolderInput')?.value.trim() || '';
  if (!folder) { setStatus('Add one folder path for this library. Paste the server/container path, such as /library/Strategy Guides.'); return; }
  const lib = { name, type, folders: [folder], lastScanned: null };
  const duplicateIndex = findExistingLibraryFolderIndex(folder, state.editingLibraryIndex);
  if (duplicateIndex >= 0) {
    state.libraries[duplicateIndex] = { ...state.libraries[duplicateIndex], ...lib };
    if (state.editingLibraryIndex !== null && state.editingLibraryIndex !== duplicateIndex) {
      state.libraries.splice(state.editingLibraryIndex, 1);
    }
    setStatus('That folder was already configured, so Guidevault updated the existing library entry instead of adding a duplicate.');
  } else if (state.editingLibraryIndex === null) {
    state.libraries.push(lib);
  } else {
    state.libraries[state.editingLibraryIndex] = lib;
  }
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
    const cleaned = [];
    const seenFolders = new Map();
    (state.libraries || []).forEach(lib => {
      const folder = String((lib?.folders || [])[0] || lib?.folder || '').trim();
      if (!folder) return;
      const entry = {
        name: lib?.name || 'Library',
        type: lib?.type || 'Manuals',
        folder,
        folders: [folder],
        lastScanned: lib?.lastScanned || null
      };
      const key = normalizeLibraryFolderForCompare(folder);
      if (seenFolders.has(key)) {
        cleaned[seenFolders.get(key)] = entry;
      } else {
        seenFolders.set(key, cleaned.length);
        cleaned.push(entry);
      }
    });
    const payload = { libraries: cleaned, operation: options.operation || '' };
    const res = await fetch('/api/settings/libraries', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(payload) });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) {
      const msg = data?.error || `Unable to save libraries. HTTP ${res.status}`;
      updateLibraryTask(localTaskId, msg, 100, 'failed', taskTitle);
      setStatus(msg);
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
    <span class="folder-browse-icon">\u25A3</span>
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
    title: 'Fast metadata enrichment',
    message: 'Requesting fast Guidevault JSON metadata enrichment...',
    progress: 2,
    kind: 'library-enrichment'
  });
  const res = await fetch('/api/library/enrich-metadata', { method: 'POST', cache: 'no-store' });
  let data = null;
  try { data = await res.json(); } catch {}
  if (!res.ok) {
    const msg = data?.error || 'Fast metadata enrichment failed. Check the terminal output.';
    updateLibraryTask(localTaskId, msg, 100, 'failed', 'Fast metadata enrichment');
    setStatus(msg);
    alert(msg);
    return;
  }
  const taskId = data?.taskId || data?.TaskId || '';
  if (taskId) {
    replaceLibraryTask(localTaskId, {
      id: taskId,
      kind: 'library-enrichment',
      title: 'Fast metadata enrichment',
      status: 'running',
      message: data?.message || 'Fast metadata enrichment queued.',
      progressPercent: 5,
      updatedAt: new Date().toISOString()
    });
  } else {
    updateLibraryTask(localTaskId, 'Fast metadata enrichment queued.', 5, 'running', 'Fast metadata enrichment');
  }
  await pollTasks(true);
}


async function importLegacyComicInfoMetadata() {
  const localTaskId = upsertLibraryTask({
    title: 'Legacy ComicInfo import',
    message: 'Requesting slower legacy ComicInfo metadata import...',
    progress: 2,
    kind: 'library-enrichment'
  });
  const res = await fetch('/api/library/enrich-comicinfo', { method: 'POST', cache: 'no-store' });
  let data = null;
  try { data = await res.json(); } catch {}
  if (!res.ok) {
    const msg = data?.error || 'Legacy ComicInfo import failed. Check the terminal output.';
    updateLibraryTask(localTaskId, msg, 100, 'failed', 'Legacy ComicInfo import');
    setStatus(msg);
    return;
  }
  const taskId = data?.taskId || data?.TaskId || '';
  if (taskId) {
    replaceLibraryTask(localTaskId, {
      id: taskId,
      kind: 'library-enrichment',
      title: 'Legacy ComicInfo import',
      status: 'running',
      message: data?.message || 'Legacy ComicInfo import queued.',
      progressPercent: 5,
      updatedAt: new Date().toISOString()
    });
  } else {
    updateLibraryTask(localTaskId, 'Legacy ComicInfo import queued.', 5, 'running', 'Legacy ComicInfo import');
  }
  await pollTasks(true);
}


function itemRecentTimestamp(item) {
  const raw = item?.added || item?.Added || item?.modified || item?.Modified || item?.updatedAt || item?.UpdatedAt || '';
  const value = new Date(raw).getTime();
  return Number.isFinite(value) ? value : 0;
}

function currentLibraryKindForSortContext() {
  const filterKind = String(state.filter || '').trim();
  if (['Manual', 'Strategy Guide', 'Magazine'].includes(filterKind)) return filterKind;
  const categoryPrefix = String(state.categoryFilter || '').split('::')[0] || '';
  if (['Manual', 'Strategy Guide', 'Magazine'].includes(categoryPrefix)) return categoryPrefix;
  const categoryMode = sidebarCategoryModeConfig(categoryPrefix);
  if (categoryMode?.kind) return categoryMode.kind;
  const customScope = String(state.customFilter?.kindScope || '').trim();
  if (['Manual', 'Strategy Guide', 'Magazine'].includes(customScope)) return customScope;
  return '';
}

function defaultLibrarySortForCurrentView() {
  const kind = currentLibraryKindForSortContext();
  if (kind === 'Magazine') return 'issue';
  if (kind === 'Manual' || kind === 'Strategy Guide') return 'title';
  return 'recent';
}

function setDefaultSortForCurrentLibraryView() {
  const select = $('sort');
  if (select) select.value = defaultLibrarySortForCurrentView();
}

function compareTextForSort(a, b) {
  return GUIDEVAULT_SORT_COLLATOR.compare(String(a || ''), String(b || ''));
}

function compareItemsByIssueThenTitle(a, b) {
  const ac = libraryItemComputed(a);
  const bc = libraryItemComputed(b);
  if (ac.hasIssue && bc.hasIssue) {
    const issueDiff = ac.issue - bc.issue;
    if (issueDiff) return issueDiff;
  }
  if (ac.hasIssue !== bc.hasIssue) return ac.hasIssue ? -1 : 1;
  return compareTextForSort(ac.title, bc.title);
}

function compareItemsForLibrarySort(a, b, sort = $('sort')?.value || defaultLibrarySortForCurrentView()) {
  const ac = libraryItemComputed(a);
  const bc = libraryItemComputed(b);
  if (sort === 'title') return compareTextForSort(ac.title, bc.title);
  if (sort === 'issue') return compareItemsByIssueThenTitle(a, b);
  if (sort === 'kind') return compareTextForSort(ac.kind, bc.kind) || compareCategoryNames(ac.kind, ac.category, bc.category) || itemSequenceThenTitle(a, b);
  if (sort === 'category') return compareCategoryNames(ac.kind, ac.category, bc.category) || itemSequenceThenTitle(a, b);
  return (bc.recent - ac.recent) || compareTextForSort(ac.title, bc.title);
}

function sortGroupNamesForCurrentSort(kind, groups, allItems) {
  const sort = $('sort')?.value || defaultLibrarySortForCurrentView();
  const summary = new Map();
  const groupLookup = new Map();
  groups.forEach(name => {
    summary.set(name, { latest: 0, count: 0 });
    groupLookup.set(sidebarCategoryCountKey(name), name);
  });
  allItems.forEach(item => {
    const seen = new Set();
    libraryCategoryKeysForItem(item).forEach(name => {
      const match = groupLookup.get(sidebarCategoryCountKey(name));
      if (!match || seen.has(match)) return;
      seen.add(match);
      const bucket = summary.get(match) || { latest: 0, count: 0 };
      bucket.count += 1;
      bucket.latest = Math.max(bucket.latest, itemRecentTimestamp(item));
      summary.set(match, bucket);
    });
  });

  return [...groups].sort((a, b) => {
    if (sort === 'recent') return ((summary.get(b)?.latest || 0) - (summary.get(a)?.latest || 0)) || compareCategoryNames(kind, a, b);
    if (sort === 'kind' || sort === 'category' || sort === 'title' || sort === 'issue') return compareCategoryNames(kind, a, b);
    return compareCategoryNames(kind, a, b) || ((summary.get(b)?.count || 0) - (summary.get(a)?.count || 0));
  });
}

function libraryItemSearchHaystack(item) {
  if (!item) return '';
  if (guidevaultLibrarySearchCache) {
    const cached = guidevaultLibrarySearchCache.get(item);
    if (cached) return cached;
  }
  const value = [
    item.title, item.kind, item.system, categoryOf(item), item.publisher, item.year, item.series,
    item.writer, item.issueNumber, item.asin, item.isbn10, item.isbn13, item.languageTag,
    platformListText(item), item.platformMatchTitle, item.platformResolverSource, item.summary,
    item.notes, item.relativePath, item.manualTitle, item.manualType, item.controlScheme,
    item.warrantySupport, ...(item.includedSections || []), ...(item.itemsCovered || []), ...(item.tags || [])
  ].join(' ').toLowerCase();
  if (guidevaultLibrarySearchCache) guidevaultLibrarySearchCache.set(item, value);
  return value;
}

function clearLibrarySearchCaches() {
  guidevaultLibrarySearchCache = typeof WeakMap === 'function' ? new WeakMap() : null;
}

function applyFilters() {
  activateDeferredFullLibrary();
  const q = ($('search')?.value || '').trim().toLowerCase();
  state.filtered = state.items.filter(item => {
    const matchesFilter = state.filter === 'All Content' || (state.filter === 'Favorites' ? isFavoriteItem(item) : item.kind === state.filter);
    if (!matchesFilter) return false;
    const matchesCategory = itemMatchesCategoryFilter(item);
    if (!matchesCategory) return false;
    const matchesCustom = !state.customFilter || customSideNavItemMatches(item, state.customFilter);
    if (!matchesCustom) return false;
    return !q || libraryItemSearchHaystack(item).includes(q);
  });

  const sort = $('sort')?.value || defaultLibrarySortForCurrentView();
  state.filtered.sort((a, b) => compareItemsForLibrarySort(a, b, sort));
  if (state.customFilter?.sortMode && state.customFilter.sortMode !== 'default') {
    if (state.customFilter.sortMode === 'title') state.filtered.sort((a,b) => compareTextForSort(displayTitle(a), displayTitle(b)));
    if (state.customFilter.sortMode === 'sequence') state.filtered.sort(itemSequenceThenTitle);
    if (state.customFilter.sortMode === 'recent') state.filtered.sort((a,b) => itemRecentTimestamp(b) - itemRecentTimestamp(a));
  }
  render();
}
function magazineThenTitle(a,b){ return itemSequenceThenTitle(a,b); }
function itemSequenceThenTitle(a,b){
  if (a?.kind === 'Magazine' || b?.kind === 'Magazine') return compareItemsByIssueThenTitle(a, b);
  return compareTextForSort(displayTitle(a), displayTitle(b));
}
function count(kind) {
  if (state.libraryIsPartial && state.librarySummary?.counts && Object.prototype.hasOwnProperty.call(state.librarySummary.counts, kind)) {
    return state.librarySummary.counts[kind] || 0;
  }
  if (!state._countCache || state._countCacheSource !== state.items) {
    const cache = { Manual: 0, 'Strategy Guide': 0, Magazine: 0 };
    (state.items || []).forEach(i => { if (Object.prototype.hasOwnProperty.call(cache, i.kind)) cache[i.kind] += 1; });
    state._countCache = cache;
    state._countCacheSource = state.items;
  }
  return state._countCache?.[kind] || 0;
}
function readCoverOverrideBustMap() {
  try {
    const parsed = JSON.parse(localStorage.getItem(GUIDEVAULT_COVER_OVERRIDE_BUST_KEY) || '{}') || {};
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

function writeCoverOverrideBustMap(map) {
  try { localStorage.setItem(GUIDEVAULT_COVER_OVERRIDE_BUST_KEY, JSON.stringify(map || {})); } catch {}
}

function setCoverOverrideBust(itemOrId, value = '') {
  const id = typeof itemOrId === 'string' ? itemOrId : (itemOrId?.id || itemOrId?.Id || '');
  const key = String(id || '').trim();
  if (!key) return '';
  const map = readCoverOverrideBustMap();
  map[key] = String(value || Date.now());
  writeCoverOverrideBustMap(map);
  return map[key];
}

function clearCoverOverrideBust(itemOrId) {
  const id = typeof itemOrId === 'string' ? itemOrId : (itemOrId?.id || itemOrId?.Id || '');
  const key = String(id || '').trim();
  if (!key) return;
  const map = readCoverOverrideBustMap();
  if (Object.prototype.hasOwnProperty.call(map, key)) {
    delete map[key];
    writeCoverOverrideBustMap(map);
  }
}

function coverUrl(item, options = {}) {
  const rawId = item?.id || item?.Id || '';
  const id = encodeURIComponent(rawId);
  if (!id) return '';
  const modified = item?.modified || item?.Modified || '';
  const size = item?.sizeBytes || item?.SizeBytes || '';
  const stamp = modified || size;
  const useFull = options?.full === true;
  const width = Math.max(120, Math.min(720, Number(options?.width || GUIDEVAULT_GRID_COVER_THUMB_WIDTH) || GUIDEVAULT_GRID_COVER_THUMB_WIDTH));
  const query = new URLSearchParams();
  if (!useFull) query.set('w', String(width));
  query.set('cv', GUIDEVAULT_APP_VERSION);
  const overrideBust = readCoverOverrideBustMap()[String(rawId || '').trim()] || item?.coverOverrideBust || item?.CoverOverrideBust || '';
  if (overrideBust) query.set('co', String(overrideBust));
  if (stamp) query.set('v', String(stamp));
  const path = useFull ? `/api/items/${id}/cover` : `/api/items/${id}/cover-thumb`;
  const qs = query.toString();
  return qs ? `${path}?${qs}` : path;
}

function loadGuidevaultCoverResultCache() {
  if (state.coverResults.loaded) return state.coverResults.cache || {};
  state.coverResults.loaded = true;
  try {
    const raw = localStorage.getItem(GUIDEVAULT_COVER_RESULT_CACHE_KEY);
    const parsed = raw ? JSON.parse(raw) : {};
    state.coverResults.cache = parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    state.coverResults.cache = {};
  }
  return state.coverResults.cache;
}

function saveGuidevaultCoverResultCacheSoon() {
  if (state.coverResults.saveTimer) return;
  state.coverResults.saveTimer = window.setTimeout(() => {
    state.coverResults.saveTimer = 0;
    try {
      const entries = Object.entries(state.coverResults.cache || {})
        .sort((a, b) => Number(b[1]?.savedAt || 0) - Number(a[1]?.savedAt || 0))
        .slice(0, GUIDEVAULT_COVER_RESULT_CACHE_LIMIT);
      localStorage.setItem(GUIDEVAULT_COVER_RESULT_CACHE_KEY, JSON.stringify(Object.fromEntries(entries)));
    } catch {}
  }, 700);
}

function setGuidevaultCoverResult(url, ok) {
  const key = String(url || '').trim();
  if (!key || key.includes('/assets/missing-cover.svg')) return;
  const cache = loadGuidevaultCoverResultCache();
  cache[key] = { ok: !!ok, savedAt: Date.now() };
  saveGuidevaultCoverResultCacheSoon();
}

function guidevaultCoverResultForUrl(url) {
  const key = String(url || '').trim();
  if (!key) return null;
  const entry = loadGuidevaultCoverResultCache()[key];
  return typeof entry?.ok === 'boolean' ? entry.ok : null;
}

function guidevaultItemLikelyHasCover(item) {
  if (!item || !itemIdOf(item)) return false;
  if (item.hasReadablePages === false || item.HasReadablePages === false) return false;
  const format = String(item.format || item.Format || '').toUpperCase();
  if (format === 'PDF') return true;
  return true;
}

function coverRetryUrl(baseUrl) {
  const separator = String(baseUrl || '').includes('?') ? '&' : '?';
  return `${baseUrl}${separator}retry=${Date.now()}`;
}

function scheduleCoverRetry(img) {
  if (!img) return;
  const baseUrl = img.dataset.coverSrc || img.src;
  const attempts = Number(img.dataset.coverAttempts || '0') || 0;
  if (!baseUrl || attempts >= 14) return;
  img.dataset.coverAttempts = String(attempts + 1);
  img.classList.add('cover-loading');
  img.classList.remove('cover-loaded', 'cover-error');
  const wrap = img.closest?.('.cover-wrap');
  wrap?.classList.add('cover-pending');
  wrap?.classList.remove('cover-ready');
  const delay = Math.min(30000, 1200 + attempts * 2200);
  window.setTimeout(() => {
    if (!img.isConnected) return;
    img.src = coverRetryUrl(baseUrl);
  }, delay);
}

function forceCoverRepaint(img) {
  if (!img || !img.isConnected) return;
  // Stability first: avoid forced layout/animation pulses while covers are loading.
  // Mark the image as ready and let the browser paint naturally.
  img.classList.add('cover-loaded');
  img.classList.remove('cover-loading', 'cover-error');
  const wrap = img.closest?.('.cover-wrap');
  wrap?.classList.add('cover-ready');
  wrap?.classList.remove('cover-pending', 'cover-error');
}

function isSecondaryCoverImage(img) {
  return String(img?.dataset?.coverPriority || '').toLowerCase() === 'secondary';
}

function setCoverFetchPriority(img) {
  if (!img) return;
  try {
    img.fetchPriority = isSecondaryCoverImage(img) ? 'low' : 'high';
  } catch {}
}

function primeCoverImage(img) {
  if (!img || !img.dataset.coverSrc) return;
  const wanted = img.dataset.coverSrc;
  const current = img.getAttribute('src') || '';
  const wrap = img.closest?.('.cover-wrap');
  wrap?.classList.add('cover-pending');
  wrap?.classList.remove('cover-ready', 'cover-error');
  img.loading = isSecondaryCoverImage(img) ? 'lazy' : 'eager';
  img.decoding = 'async';
  setCoverFetchPriority(img);
  img.classList.add('cover-loading');
  img.classList.remove('cover-loaded', 'cover-error');
  if (current !== wanted) {
    img.setAttribute('src', wanted);
    return;
  }
  if (img.complete && img.naturalWidth > 1 && img.naturalHeight > 1) {
    forceCoverRepaint(img);
    scheduleColorscapeSampleForLoadedCover(img);
    return;
  }
  if (typeof img.decode === 'function') {
    img.decode().then(() => {
      forceCoverRepaint(img);
      scheduleColorscapeSampleForLoadedCover(img);
    }).catch(() => {});
  }
}

function coverImageIsNearViewport(img, padding = 180) {
  const scroller = libraryScrollElement();
  const scrollerRect = scroller?.getBoundingClientRect?.();
  const rect = img?.getBoundingClientRect?.();
  if (!rect || !scrollerRect) return true;
  return rect.bottom >= scrollerRect.top - padding && rect.top <= scrollerRect.bottom + padding;
}

function flushPrimaryCoverPrimeQueue() {
  guidevaultPrimaryCoverPrimeTimer = 0;
  const batch = [];
  for (const img of guidevaultPrimaryCoverPrimeQueue) {
    guidevaultPrimaryCoverPrimeQueue.delete(img);
    if (!img?.isConnected || !coverImageIsNearViewport(img, GUIDEVAULT_COVER_VIEWPORT_PRIME_PADDING + 120)) continue;
    batch.push(img);
    if (batch.length >= GUIDEVAULT_PRIMARY_COVER_BATCH_SIZE) break;
  }
  batch.forEach(primeCoverImage);
  if (guidevaultPrimaryCoverPrimeQueue.size) {
    guidevaultPrimaryCoverPrimeTimer = window.setTimeout(flushPrimaryCoverPrimeQueue, GUIDEVAULT_PRIMARY_COVER_DELAY_MS);
  }
}

function queuePrimaryCoverPrime(img) {
  if (!img || !img.dataset.coverSrc) return;
  if ((img.getAttribute('src') || '') === img.dataset.coverSrc) return;
  guidevaultPrimaryCoverPrimeQueue.add(img);
  if (guidevaultPrimaryCoverPrimeTimer) return;
  guidevaultPrimaryCoverPrimeTimer = window.setTimeout(flushPrimaryCoverPrimeQueue, 0);
}

function flushSecondaryCoverPrimeQueue() {
  guidevaultSecondaryCoverPrimeTimer = 0;
  const batch = [];
  for (const img of guidevaultSecondaryCoverPrimeQueue) {
    guidevaultSecondaryCoverPrimeQueue.delete(img);
    if (!img?.isConnected || !coverImageIsNearViewport(img, 220)) continue;
    batch.push(img);
    if (batch.length >= GUIDEVAULT_SECONDARY_COVER_BATCH_SIZE) break;
  }
  batch.forEach(primeCoverImage);
  if (guidevaultSecondaryCoverPrimeQueue.size) {
    guidevaultSecondaryCoverPrimeTimer = window.setTimeout(flushSecondaryCoverPrimeQueue, 180);
  }
}

function queueSecondaryCoverPrime(img) {
  if (!img || !img.dataset.coverSrc) return;
  if ((img.getAttribute('src') || '') === img.dataset.coverSrc) return;
  guidevaultSecondaryCoverPrimeQueue.add(img);
  if (guidevaultSecondaryCoverPrimeTimer) return;
  guidevaultSecondaryCoverPrimeTimer = window.setTimeout(flushSecondaryCoverPrimeQueue, GUIDEVAULT_SECONDARY_COVER_DELAY_MS);
}

function flushCategoryPrimaryPrewarmQueue() {
  guidevaultCategoryPrimaryPrewarmTimer = 0;
  const batch = guidevaultCategoryPrimaryPrewarmQueue.splice(0, GUIDEVAULT_CATEGORY_PRIMARY_PREWARM_BATCH_SIZE);
  batch.forEach(url => {
    if (!url) return;
    try {
      const img = new Image();
      img.decoding = 'async';
      img.loading = 'eager';
      try { img.fetchPriority = 'high'; } catch {}
      img.src = url;
    } catch {}
  });
  if (guidevaultCategoryPrimaryPrewarmQueue.length) {
    guidevaultCategoryPrimaryPrewarmTimer = window.setTimeout(flushCategoryPrimaryPrewarmQueue, GUIDEVAULT_CATEGORY_PRIMARY_PREWARM_DELAY_MS);
  }
}

function enqueueCategoryPrimaryCoverPrewarm(url) {
  const clean = String(url || '').trim();
  if (!clean || guidevaultCategoryPrimaryPrewarmSeen.has(clean)) return;
  guidevaultCategoryPrimaryPrewarmSeen.add(clean);
  guidevaultCategoryPrimaryPrewarmQueue.push(clean);
  if (guidevaultCategoryPrimaryPrewarmTimer) return;
  guidevaultCategoryPrimaryPrewarmTimer = window.setTimeout(flushCategoryPrimaryPrewarmQueue, 40);
}

function collectPrimaryCategoryCoverUrls(kind, limit = GUIDEVAULT_CATEGORY_PRIMARY_PREWARM_LIMIT) {
  const targetKind = String(kind || '').trim();
  if (!targetKind || !Array.isArray(state.items) || !state.items.length) return [];
  const groupMap = new Map();
  for (const item of state.items) {
    if (item?.kind !== targetKind) continue;
    const keys = libraryCategoryKeysForItem(item);
    for (const name of keys) {
      const label = String(name || '').trim();
      const key = sidebarCategoryCountKey(label);
      if (!key) continue;
      if (!groupMap.has(key)) groupMap.set(key, { name: label, items: [] });
      groupMap.get(key).items.push(item);
    }
  }
  const groups = sortGroupNamesForCurrentSort(
    targetKind,
    sortCategoriesForKind(targetKind, [...groupMap.values()].map(group => group.name)),
    state.items.filter(i => i.kind === targetKind)
  );
  const urls = [];
  for (const name of groups) {
    const bucket = groupMap.get(sidebarCategoryCountKey(name));
    const items = [...(bucket?.items || [])].sort(targetKind === 'Manual' ? ((a,b)=>compareTextForSort(displayTitle(a), displayTitle(b))) : itemSequenceThenTitle);
    const url = coverUrl(items.find(item => coverUrl(item)) || items[0] || {});
    if (url) urls.push(url);
    if (urls.length >= limit) break;
  }
  return urls;
}

function scheduleCategoryPrimaryCoverPrewarm(kind = '') {
  scheduleCategoryPreviewCoverPrewarm(kind, { includeSecondary: false });
}

function compareCategoryPreviewCandidates(a, b) {
  const aUrl = coverUrl(a);
  const bUrl = coverUrl(b);
  const aKnown = guidevaultCoverResultForUrl(aUrl);
  const bKnown = guidevaultCoverResultForUrl(bUrl);
  const aRank = (aKnown === true ? 0 : aKnown === null ? 1 : 3) + (guidevaultItemLikelyHasCover(a) ? 0 : 20);
  const bRank = (bKnown === true ? 0 : bKnown === null ? 1 : 3) + (guidevaultItemLikelyHasCover(b) ? 0 : 20);
  if (aRank !== bRank) return aRank - bRank;
  return itemSequenceThenTitle(a, b);
}

function categoryPreviewItems(items, limit = 4) {
  const seen = new Set();
  return [...(items || [])]
    .filter(item => {
      const id = itemIdOf(item);
      if (!id || seen.has(id)) return false;
      seen.add(id);
      return guidevaultItemLikelyHasCover(item);
    })
    .sort(compareCategoryPreviewCandidates)
    .slice(0, limit);
}

function collectCategoryPreviewCoverUrls(kind = '', options = {}) {
  const targetKinds = kind ? [kind] : ['Manual', 'Strategy Guide', 'Magazine'];
  const includeSecondary = options.includeSecondary !== false;
  const limit = Math.max(24, Number(options.limit || GUIDEVAULT_CATEGORY_PREVIEW_PREWARM_LIMIT));
  const urls = [];
  const seen = new Set();
  targetKinds.forEach(targetKind => {
    const groupMap = new Map();
    for (const item of state.items || []) {
      if (item?.kind !== targetKind) continue;
      for (const name of libraryCategoryKeysForItem(item)) {
        const label = String(name || '').trim();
        const key = sidebarCategoryCountKey(label);
        if (!key) continue;
        if (!groupMap.has(key)) groupMap.set(key, { name: label, items: [] });
        groupMap.get(key).items.push(item);
      }
    }
    const groups = sortGroupNamesForCurrentSort(
      targetKind,
      sortCategoriesForKind(targetKind, [...groupMap.values()].map(group => group.name)),
      (state.items || []).filter(i => i.kind === targetKind)
    );
    for (const name of groups) {
      const bucket = groupMap.get(sidebarCategoryCountKey(name));
      const candidates = categoryPreviewItems(bucket?.items || [], includeSecondary ? 4 : 1);
      for (const item of candidates) {
        const url = coverUrl(item);
        if (!url || seen.has(url)) continue;
        seen.add(url);
        urls.push(url);
        if (urls.length >= limit) return;
      }
      if (urls.length >= limit) return;
    }
  });
  return urls;
}

function flushCategoryPreviewPrewarmQueue() {
  guidevaultCategoryPreviewPrewarmTimer = 0;
  const batch = guidevaultCategoryPreviewPrewarmQueue.splice(0, GUIDEVAULT_CATEGORY_PREVIEW_PREWARM_BATCH_SIZE);
  batch.forEach(url => {
    try {
      const img = new Image();
      img.decoding = 'async';
      img.loading = 'eager';
      try { img.fetchPriority = 'high'; } catch {}
      img.onload = () => setGuidevaultCoverResult(url, !(String(img.currentSrc || img.src || '').toLowerCase().includes('/assets/missing-cover.svg')));
      img.onerror = () => setGuidevaultCoverResult(url, false);
      img.src = url;
    } catch {}
  });
  if (guidevaultCategoryPreviewPrewarmQueue.length) {
    guidevaultCategoryPreviewPrewarmTimer = window.setTimeout(flushCategoryPreviewPrewarmQueue, GUIDEVAULT_CATEGORY_PREVIEW_PREWARM_DELAY_MS);
  }
}

function enqueueCategoryPreviewCoverPrewarm(url) {
  const clean = String(url || '').trim();
  if (!clean || guidevaultCategoryPreviewPrewarmSeen.has(clean)) return;
  guidevaultCategoryPreviewPrewarmSeen.add(clean);
  guidevaultCategoryPreviewPrewarmQueue.push(clean);
  if (guidevaultCategoryPreviewPrewarmTimer) return;
  guidevaultCategoryPreviewPrewarmTimer = window.setTimeout(flushCategoryPreviewPrewarmQueue, 1);
}

function requestServerCategoryCoverPrewarm(kind = '') {
  const token = `${kind || 'all'}:${Math.floor(Date.now() / 12000)}`;
  if (state.coverResults.serverPrewarmToken === token) return;
  state.coverResults.serverPrewarmToken = token;
  const params = new URLSearchParams({ limit: String(GUIDEVAULT_CATEGORY_PREVIEW_PREWARM_LIMIT) });
  if (kind) params.set('kind', kind);
  fetch(`/api/library/prewarm-covers?${params.toString()}`, { method: 'POST', cache: 'no-store', keepalive: true }).catch(() => {});
}

function scheduleCategoryPreviewCoverPrewarm(kind = '', options = {}) {
  // 0.9.132: disabled aggressive category preview prewarming.  It made the
  // first library click feel slower by competing with the covers the user can
  // actually see.  Category menus now load only the first visible primary
  // covers eagerly, then let the normal viewport observer handle the rest.
}

let coverPrimeObserver = null;
function ensureCoverPrimeObserver() {
  if (coverPrimeObserver || typeof IntersectionObserver !== 'function') return coverPrimeObserver;
  coverPrimeObserver = new IntersectionObserver(entries => {
    entries.forEach(entry => {
      if (!(entry.isIntersecting || entry.intersectionRatio > 0)) return;
      if (isSecondaryCoverImage(entry.target)) queueSecondaryCoverPrime(entry.target);
      else queuePrimaryCoverPrime(entry.target);
    });
  }, { root: libraryScrollElement(), rootMargin: `${GUIDEVAULT_COVER_VIEWPORT_PRIME_PADDING}px 0px ${GUIDEVAULT_COVER_VIEWPORT_PRIME_PADDING}px 0px`, threshold: 0.01 });
  return coverPrimeObserver;
}

function primeVisibleCoverImages(root = document) {
  const primary = [];
  const secondary = [];
  root.querySelectorAll?.('img[data-cover-src]').forEach(img => {
    if (!coverImageIsNearViewport(img, GUIDEVAULT_COVER_VIEWPORT_PRIME_PADDING)) return;
    if (isSecondaryCoverImage(img)) secondary.push(img);
    else primary.push(img);
  });
  primary.forEach(queuePrimaryCoverPrime);
  secondary.forEach(queueSecondaryCoverPrime);
}

function initializeCoverImages(root = document) {
  const observer = ensureCoverPrimeObserver();
  root.querySelectorAll?.('img[data-cover-src]').forEach(img => {
    if (img.dataset.coverWatch !== '1') {
      img.dataset.coverWatch = '1';
      img.loading = 'lazy';
      img.decoding = 'async';
      setCoverFetchPriority(img);
      img.classList.add('cover-loading');
      img.closest?.('.cover-wrap')?.classList.add('cover-pending');
      img.addEventListener('error', () => {
        img.classList.add('cover-error');
        img.classList.remove('cover-loaded', 'cover-loading');
        const wrap = img.closest?.('.cover-wrap');
        wrap?.classList.add('cover-error');
        wrap?.classList.remove('cover-ready');
        img.closest?.('.category-preview-strip')?.classList.add('category-preview-missing');
        scheduleCoverRetry(img);
      });
      img.addEventListener('load', () => {
        const current = String(img.currentSrc || img.src || '').toLowerCase();
        const requested = img.dataset.coverSrc || '';
        if (current.includes('/assets/missing-cover.svg')) {
          setGuidevaultCoverResult(requested, false);
          img.classList.add('cover-missing');
          img.classList.remove('cover-loaded', 'cover-loading');
          const wrap = img.closest?.('.cover-wrap');
          wrap?.classList.add('cover-error');
          wrap?.classList.remove('cover-ready', 'cover-pending');
          img.closest?.('.category-preview-strip')?.classList.add('category-preview-missing');
          return;
        }
        if (img.naturalWidth > 1 && img.naturalHeight > 1) {
          setGuidevaultCoverResult(requested, true);
          img.classList.remove('cover-missing');
          img.closest?.('.category-preview-strip')?.classList.remove('category-preview-missing');
          forceCoverRepaint(img);
          scheduleColorscapeSampleForLoadedCover(img);
        }
      });
      observer?.observe(img);
    }
    if (img.complete && img.naturalWidth > 1 && img.naturalHeight > 1) {
      forceCoverRepaint(img);
      scheduleColorscapeSampleForLoadedCover(img);
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
function scrollTopForElementWithinScroller(element, scroller) {
  if (!element || !scroller) return 0;
  try {
    const elementRect = element.getBoundingClientRect();
    const scrollerRect = scroller.getBoundingClientRect();
    return Math.max(0, (elementRect.top - scrollerRect.top) + scroller.scrollTop);
  } catch {
    return Math.max(0, Number(element.offsetTop || 0));
  }
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
  if (target) {
    const scrollerRect = scroller.getBoundingClientRect();
    const targetRect = target.getBoundingClientRect();
    const top = targetRect.top - scrollerRect.top + scroller.scrollTop - 10;
    scroller.scrollTo({ top: Math.max(0, top), behavior: 'smooth' });
    return;
  }
  const vgrid = state.virtualGrid;
  if (vgrid?.items?.length && vgrid.host === grid) {
    const index = vgrid.items.findIndex(item => alphaKey(displayTitle(item)) === key);
    if (index >= 0) {
      const columns = Math.max(1, Number(vgrid.columns || estimateGridColumns(grid)));
      const row = Math.floor(index / columns);
      const rowHeight = Math.max(160, Number(vgrid.rowHeight || 300));
      const gridTop = scrollTopForElementWithinScroller(grid, scroller);
      scroller.scrollTo({ top: Math.max(0, gridTop + row * rowHeight - 10), behavior: 'smooth' });
      requestAnimationFrame(() => renderVirtualGridWindow(true));
    }
  }
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

  $('itemCount').textContent = groupMode ? `${groupCountForView()} categories` : `${currentLibraryTotalCount()} items${state.libraryIsPartial ? ' indexed' : ''}`;
  $('libraryView').classList.toggle('category-mode', categoryMode || groupMode || state.viewMode !== 'all');
  $('libraryView').classList.toggle('group-mode', groupMode);
  $('libraryView').classList.toggle('magazine-mode', state.filter === 'Magazine' || state.viewMode === 'magazine-series');

  $('pageTitle').textContent = pageTitleForView();
  $('gridTitle').textContent = groupMode ? pageTitleForView() : (state.customFilter ? `${pageTitleForView()} Results` : (categoryMode ? `${currentCategoryName()} Library` : 'Home Library'));
  $('manualSummary').textContent = `${count('Manual')} items`;
  $('guideSummary').textContent = `${count('Strategy Guide')} items`;
  $('magSummary').textContent = `${count('Magazine')} items`;
  scheduleRenderCategories();

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
    return years.length ? `${years[0]}${years.length > 1 ? ` \u2013 ${years[years.length - 1]}` : ''}` : 'Magazine run';
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
function escapeForCssString(value) {
  return String(value ?? '')
    .replace(/\\/g, '\\\\')
    .replace(/'/g, "\\'")
    .replace(/[\r\n\f]/g, '');
}

function groupGridCacheKey(kind, sort) {
  return `${Number(state.libraryCategoryCacheVersion || 0)}|${String(kind || '')}|${String(sort || '')}`;
}

function getCachedGroupGridData(kind, sort) {
  const currentVersion = Number(state.libraryCategoryCacheVersion || 0);
  if (guidevaultGroupGridCacheVersion !== currentVersion) {
    guidevaultGroupGridCacheVersion = currentVersion;
    guidevaultGroupGridCache = new Map();
  }
  const key = groupGridCacheKey(kind, sort);
  const cached = guidevaultGroupGridCache.get(key);
  if (cached) return cached;

  const allKindItems = [];
  const groupMap = new Map();
  for (const item of state.items || []) {
    if (item?.kind !== kind) continue;
    allKindItems.push(item);
    const seen = new Set();
    for (const name of libraryCategoryKeysForItem(item)) {
      const label = String(name || '').trim();
      const groupKey = sidebarCategoryCountKey(label);
      if (!groupKey || seen.has(groupKey)) continue;
      seen.add(groupKey);
      if (!groupMap.has(groupKey)) groupMap.set(groupKey, { name: label, items: [] });
      groupMap.get(groupKey).items.push(item);
    }
  }
  const groupNames = [...groupMap.values()].map(group => group.name);
  const groups = sortGroupNamesForCurrentSort(kind, sortCategoriesForKind(kind, groupNames), allKindItems);
  const result = { allKindItems, groupMap, groups };
  guidevaultGroupGridCache.set(key, result);
  return result;
}

function openCategoryGridCard(card) {
  if (!card) return;
  showLibraryScreen();
  state.filter = card.dataset.kind;
  state.categoryFilter = `${card.dataset.kind}::${card.dataset.category}`;
  state.viewMode = 'category';
  if ($('search')) $('search').value = '';
  setDefaultSortForCurrentLibraryView();
  updateNavActive();
  scrollMainToTop();
  applyFilters();
}

function wireGroupGridCategoryDelegation(host) {
  if (!host || host.dataset.groupGridDelegated === '1') return;
  host.dataset.groupGridDelegated = '1';
  host.addEventListener('click', event => {
    const card = event.target.closest?.('.category-card.category-card-redesign[data-kind][data-category]');
    if (!card || !host.contains(card)) return;
    openCategoryGridCard(card);
  });
}

function categoryPreviewStripHtml(items, name, cardIndex = 0, kind = '') {
  const covers = categoryPreviewItems(items, 1);
  if (!covers.length) {
    return `<div class="category-preview-strip single-cover" aria-hidden="true"><div class="category-preview-empty">${categoryDisplayIconHtml(kind, name, 'platform-icon large')}</div></div>`;
  }
  const item = covers[0];
  const url = coverUrl(item);
  const eager = Number(cardIndex || 0) < GUIDEVAULT_CATEGORY_VISIBLE_COVER_EAGER_LIMIT;
  const src = eager ? url : GUIDEVAULT_TRANSPARENT_COVER_PLACEHOLDER;
  const coverVar = url ? ` style="--gv-category-preview-cover:url('${escapeForAttribute(escapeForCssString(url))}')"` : '';
  const cover = `<img class="category-preview-primary-cover" decoding="async" loading="${eager ? 'eager' : 'lazy'}" fetchpriority="${eager ? 'high' : 'low'}" data-cover-priority="primary" data-cover-src="${escapeForAttribute(url)}" src="${escapeForAttribute(src)}" alt="${escapeHtml(displayTitle(item))} cover" />`;
  return `<div class="category-preview-strip single-cover faux-three-cover" aria-hidden="true"${coverVar}>${cover}</div>`;
}
function renderGroupGrid(id, viewMode) {
  clearVirtualGridIfHost(id);
  const def = groupDefinition(viewMode);
  const axis = groupAxisLabelForKind(def.kind);
  const currentSort = $('sort')?.value || defaultLibrarySortForCurrentView();
  const { allKindItems, groupMap, groups } = getCachedGroupGridData(def.kind, currentSort);
  renderAlphaRail(groups);
  const overview = groups.length ? `<section class="group-hub-panel">
      <div class="group-hub-copy">
        <span>${escapeHtml(def.kind)} Library</span>
        <h2>Browse by ${escapeHtml(axis === 'publication' ? 'publication' : 'platform')}</h2>
        <p>${escapeHtml(def.kind === 'Magazine'
          ? 'Magazine runs are grouped by publication so issues stay together and remain easier to scan.'
          : 'Content is grouped by platform so each library tile opens a focused shelf of related entries.')}</p>
      </div>
    </section>` : '';
  const cards = groups.map((name, cardIndex) => {
    const bucket = groupMap.get(sidebarCategoryCountKey(name));
    const items = [...(bucket?.items || [])].sort(def.kind === 'Manual' ? ((a,b)=>compareTextForSort(displayTitle(a), displayTitle(b))) : itemSequenceThenTitle);
    const issueHint = def.kind === 'Magazine' ? sequenceRange(items) : `${items.length} ${items.length === 1 ? 'entry' : 'entries'}`;
    const specialCategoryClass = def.kind === 'Manual' && isNintendoEntertainmentSystemName(name) ? ' nes-manual-category' : '';
    const latest = groupCardLatestLabel(items);
    const secondary = groupCardSecondaryLabel(def.kind, name, items);
    const colorscapeCover = coverUrl(items.find(item => coverUrl(item)) || items[0] || {});
    return `<article class="category-card category-card-redesign${specialCategoryClass}" data-kind="${escapeHtml(def.kind)}" data-category="${escapeForAttribute(name)}" data-alpha="${alphaKey(name)}" data-colorscape-cover="${escapeForAttribute(colorscapeCover)}">
      <div class="category-card-content">
        <div class="category-title-line">
          <span class="category-platform-mark">${categoryDisplayIconHtml(def.kind, name, 'platform-icon large')}</span>
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
      ${categoryPreviewStripHtml(items, name, cardIndex, def.kind)}
    </article>`;
  }).join('');
  $(id).innerHTML = overview + (cards || `<div class="empty-message">${def.empty} Set a Library Root folder in Settings and scan your collection.</div>`);
  const host = $(id);
  initializeCoverImages(host);
  attachCoverPrimeScrollHandler();
  scheduleApplyColorscapeToGroupCards(def.kind);
  wireGroupGridCategoryDelegation(host);
}

function sequenceRange(items) {
  const values = items.map(issueValue).filter(n => Number.isFinite(n) && n > 0).sort((a,b)=>a-b);
  if (!values.length) return `${items.length} ${items.length === 1 ? 'issue' : 'issues'}`;
  const first = values[0]; const last = values[values.length - 1];
  return first === last ? `Issue #${first}` : `Issues #${first}\u2013${last}`;
}
function sidebarSplitValues(value) {
  const rawValues = Array.isArray(value) ? value : String(value || '').split(/[;,|]/);
  const result = [];
  rawValues.forEach(raw => {
    const text = String(raw || '').trim();
    if (!text || text === '\u2014' || /^unknown$/i.test(text)) return;
    if (!result.some(existing => existing.localeCompare(text, undefined, { sensitivity: 'accent' }) === 0)) result.push(text);
  });
  return result;
}

function sidebarPushCategory(values, value) {
  const text = String(value || '').trim();
  if (!text || text === '\u2014' || /^unknown$/i.test(text)) return;
  if (!values.some(existing => existing.localeCompare(text, undefined, { sensitivity: 'accent' }) === 0)) values.push(text);
}

function sidebarYearLabelForItem(item) {
  const raw = item?.magazineYear || item?.year || item?.coverDate || item?.releaseDate || '';
  const year = String(raw || '').match(/\d{4}/)?.[0] || '';
  return year || 'Unknown Year';
}

function sidebarGuideTypeValues(item) {
  const values = sidebarSplitValues(item?.guideType || item?.guideTypes || item?.strategyGuideType || item?.type);
  return values.length ? values : ['Unsorted Guide Type'];
}

function sidebarCategoryModeConfig(mode) {
  return SIDEBAR_CATEGORY_MODE_CONFIGS[mode] || null;
}

function sidebarCategoryValuesForItem(item, mode) {
  const config = sidebarCategoryModeConfig(mode);
  if (!item || !config?.valueForItem) return [];
  const values = [];
  const raw = config.valueForItem(item);
  (Array.isArray(raw) ? raw : [raw]).forEach(value => sidebarPushCategory(values, value));
  if (!values.length && config.fallback) values.push(config.fallback);
  return values;
}

const SIDEBAR_CATEGORY_MODE_CONFIGS = {
  'manual-system': {
    kind: 'Manual',
    label: 'Manual Systems',
    empty: 'No manual systems found yet.',
    fallback: 'Unsorted Manuals',
    iconFor: category => platformIconHtml(category),
    valueForItem: item => normalizeGuidevaultPlatformName(item?.category || item?.system || item?.primarySystem || '')
  },
  'manual-series': {
    kind: 'Manual',
    label: 'Manual Series',
    empty: 'No manual series or franchise values found yet.',
    fallback: 'Unsorted Manual Series',
    iconFor: () => '<span class="category-mini-icon" aria-hidden="true">\u25E6</span>',
    valueForItem: item => item?.franchise || item?.gameFranchise || item?.series || ''
  },
  'strategy-platform': {
    kind: 'Strategy Guide',
    label: 'Strategy Platforms',
    empty: 'No strategy guide platform values found yet.',
    fallback: 'Unsorted Strategy Guides',
    iconFor: category => platformIconHtml(category),
    valueForItem: item => {
      const platforms = associatedPlatformsOf(item);
      if (platforms.length) return platforms;
      return normalizeGuidevaultPlatformName(item?.category || item?.system || item?.primarySystem || '');
    }
  },
  'strategy-guide-type': {
    kind: 'Strategy Guide',
    label: 'Strategy Guide Types',
    empty: 'No strategy guide types found yet.',
    fallback: 'Unsorted Guide Type',
    iconFor: () => '<span class="category-mini-icon" aria-hidden="true">\u25C8</span>',
    valueForItem: item => sidebarGuideTypeValues(item)
  },
  'strategy-series': {
    kind: 'Strategy Guide',
    label: 'Strategy Game Series',
    empty: 'No strategy guide series or franchise values found yet.',
    fallback: 'Unsorted Strategy Series',
    iconFor: () => '<span class="category-mini-icon" aria-hidden="true">\u25E6</span>',
    valueForItem: item => item?.franchise || item?.gameFranchise || item?.series || ''
  },
  'magazine-title': {
    kind: 'Magazine',
    label: 'Magazine Titles',
    empty: 'No magazine titles found yet.',
    fallback: 'Unsorted Magazines',
    iconFor: () => '<span class="category-mini-icon" aria-hidden="true">\u25A6</span>',
    valueForItem: item => item?.magazineTitle || item?.series || ''
  },
  'magazine-primary-system': {
    kind: 'Magazine',
    label: 'Magazine Primary Systems',
    empty: 'No magazine primary systems found yet.',
    fallback: 'Unsorted Primary System',
    iconFor: category => platformIconHtml(category),
    valueForItem: item => normalizeGuidevaultPlatformName(item?.primarySystem || '')
  },
  'magazine-year': {
    kind: 'Magazine',
    label: 'Magazine Years',
    empty: 'No magazine years found yet.',
    fallback: 'Unknown Year',
    iconFor: () => '<span class="category-mini-icon" aria-hidden="true">\u25F7</span>',
    valueForItem: item => sidebarYearLabelForItem(item)
  },
  'metadata-status': {
    label: 'Metadata Status',
    empty: 'No metadata status values found yet.',
    fallback: 'Unreviewed',
    iconFor: () => '<span class="category-mini-icon" aria-hidden="true">\u25C6</span>',
    valueForItem: item => metadataStatusOf(item)
  }
};

function normalizeCategoryStructure(value) {
  const allowed = new Set(['content-type', 'platform', 'publisher', 'decade', ...Object.keys(SIDEBAR_CATEGORY_MODE_CONFIGS)]);
  return allowed.has(value) ? value : 'content-type';
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
function isCategoryGroupCollapsed(key, defaultCollapsed = true) {
  const stored = state.collapsedCategoryGroups?.[key];
  return typeof stored === 'boolean' ? stored : defaultCollapsed;
}

function sidebarCategoryCountKey(value = '') {
  return String(value || '').trim().toLocaleLowerCase();
}

function buildSidebarCategoryCountMap(items = [], valueSelector = () => []) {
  const counts = new Map();
  (Array.isArray(items) ? items : []).forEach(item => {
    const raw = valueSelector(item);
    const values = Array.isArray(raw) ? raw : [raw];
    const seen = new Set();
    values.map(value => String(value || '').trim()).filter(Boolean).forEach(value => {
      const key = sidebarCategoryCountKey(value);
      if (!key || seen.has(key)) return;
      seen.add(key);
      counts.set(key, (counts.get(key) || 0) + 1);
    });
  });
  return counts;
}

function sidebarCategoryCount(counts, category) {
  return counts?.get?.(sidebarCategoryCountKey(category)) || 0;
}

function categoryIsMagazinePublicationOnly(category) {
  const target = sidebarCategoryCountKey(category);
  if (!target) return false;
  let magazineMatch = false;
  let nonMagazineMatch = false;
  for (const item of state.items || []) {
    const matches = libraryCategoryKeysForItem(item).some(name => sidebarCategoryCountKey(name) === target);
    if (!matches) continue;
    if (item.kind === 'Magazine') magazineMatch = true;
    else nonMagazineMatch = true;
    if (magazineMatch && nonMagazineMatch) return false;
  }
  return magazineMatch && !nonMagazineMatch;
}
function categoryGroupMarkup(key, label, items, categories, options = {}) {
  const defaultCollapsed = options.defaultCollapsed !== undefined ? !!options.defaultCollapsed : true;
  const collapsed = isCategoryGroupCollapsed(key, defaultCollapsed);
  const groupKind = options.groupKind || key;
  const countFor = options.countFor || ((category) => items.filter(i => libraryCategoryKeysForItem(i).some(name => name.localeCompare(category, undefined, { sensitivity: 'accent' }) === 0)).length);
  const iconFor = options.iconFor || ((category) => platformIconHtml(category));
  const empty = options.empty || '';
  const body = collapsed
    ? ''
    : (categories.length ? categories.map(c => {
      const category = String(c || '').trim();
      const filterKey = `${groupKind}::${category}`;
      const active = state.categoryFilter === filterKey ? ' active' : '';
      const countForCategory = countFor(category);
      return `<button class="system-btn${active}" data-kind="${escapeHtml(groupKind)}" data-category="${escapeHtml(category)}" title="${escapeHtml(label)}: ${escapeHtml(category)}"><span class="system-label">${iconFor(category)}<span>${escapeHtml(category)}</span></span><em>${countForCategory}</em></button>`;
    }).join('') : `<p class="sub small-pad">${escapeHtml(empty || 'No categories found yet.')}</p>`);
  return `<div class="category-group${collapsed ? ' collapsed' : ''}" data-group-kind="${escapeHtml(key)}">
      <button type="button" class="category-group-toggle" data-kind="${escapeHtml(key)}" data-default-collapsed="${defaultCollapsed ? 'true' : 'false'}" aria-expanded="${collapsed ? 'false' : 'true'}" title="${collapsed ? 'Expand' : 'Collapse'} ${escapeHtml(label)}">
        <span class="collapse-mark" aria-hidden="true">${collapsed ? '\u25B8' : '\u25BE'}</span>
        <span class="category-group-label">${escapeHtml(label)}</span>
        <em>${items.length}</em>
      </button>
      <div class="category-body">${body}</div>
    </div>`;
}
function categoryRenderCacheKey(structure) {
  return [
    Number(state.libraryCategoryCacheVersion || 0),
    structure,
    state.categoryFilter || '',
    JSON.stringify(state.collapsedCategoryGroups || {})
  ].join('|');
}

function wireCategorySidebar(host) {
  if (!host || host.dataset.categoryWired === '1') return;
  host.dataset.categoryWired = '1';
  host.addEventListener('click', event => {
    const toggle = event.target.closest?.('.category-group-toggle');
    if (toggle && host.contains(toggle)) {
      const kind = toggle.dataset.kind;
      const defaultCollapsed = toggle.dataset.defaultCollapsed !== 'false';
      state.collapsedCategoryGroups[kind] = !isCategoryGroupCollapsed(kind, defaultCollapsed);
      renderCategories();
      return;
    }

    const btn = event.target.closest?.('.system-btn');
    if (!btn || !host.contains(btn)) return;
    showLibraryScreen();
    const kind = btn.dataset.kind || 'Any';
    const mode = sidebarCategoryModeConfig(kind);
    state.filter = mode?.kind || (['Any', 'Publisher', 'Decade'].includes(kind) ? 'All Content' : kind);
    state.categoryFilter = `${kind}::${btn.dataset.category}`;
    state.customFilter = null;
    state.viewMode = 'category';
    if ($('search')) $('search').value = '';
    setDefaultSortForCurrentLibraryView();
    updateNavActive();
    document.querySelectorAll('.chip').forEach(c => c.classList.toggle('active', c.dataset.kind === state.filter));
    scrollMainToTop();
    applyFilters();
  });
}

function scheduleRenderCategories() {
  const host = $('categories');
  if (!host) return;
  window.clearTimeout(guidevaultCategoryRenderTimer);
  guidevaultCategoryRenderTimer = window.setTimeout(() => {
    scheduleGuidevaultIdleWork(() => {
      guidevaultCategoryRenderTimer = 0;
      renderCategories();
    });
  }, 20);
}

function renderCategories() {
  const host = $('categories');
  if (!host) return;
  wireCategorySidebar(host);
  const structure = loadCategoryStructure();
  const cacheKey = categoryRenderCacheKey(structure);
  if (guidevaultCategoryRenderKey === cacheKey && host.dataset.categoryCacheKey === cacheKey) return;
  const groups = [['Manual', 'Manuals'], ['Strategy Guide', 'Strategy Guides'], ['Magazine', 'Magazines']];
  let markup = '';
  if (structure === 'content-type') {
    markup = groups.map(([kind, label]) => {
      const items = state.items.filter(i => i.kind === kind);
      const counts = buildSidebarCategoryCountMap(items, libraryCategoryKeysForItem);
      const categories = sortCategoriesForKind(kind, [...new Set(items.flatMap(libraryCategoryKeysForItem))]);
      if (!categories.length) return '';
      return categoryGroupMarkup(kind, label, items, categories, {
        groupKind: kind,
        iconFor: kind === 'Magazine' ? (() => magazinePublicationIconHtml('category-mini-icon magazine-publication-icon')) : undefined,
        countFor: category => sidebarCategoryCount(counts, category),
        empty: `No ${label.toLowerCase()} categories found yet.`
      });
    }).join('');
  } else if (structure === 'platform') {
    const counts = buildSidebarCategoryCountMap(state.items, libraryCategoryKeysForItem);
    const categories = sortCategoriesForKind('Any', [...new Set(state.items.flatMap(libraryCategoryKeysForItem))]);
    markup = categoryGroupMarkup('Any', 'Platforms / Publications', state.items, categories, {
      groupKind: 'Any',
      iconFor: category => categoryIsMagazinePublicationOnly(category) ? magazinePublicationIconHtml('category-mini-icon magazine-publication-icon') : platformIconHtml(category),
      countFor: category => sidebarCategoryCount(counts, category),
      empty: 'No platforms or publications found yet.',
      defaultCollapsed: false
    });
  } else if (structure === 'publisher') {
    const publisherValue = item => String(item.publisher || 'Unsorted Publisher').trim() || 'Unsorted Publisher';
    const publisherCounts = buildSidebarCategoryCountMap(state.items, publisherValue);
    const publishers = [...new Set(state.items.map(publisherValue).filter(Boolean))].sort((a,b)=>a.localeCompare(b));
    markup = categoryGroupMarkup('Publisher', 'Publishers', state.items, publishers, {
      groupKind: 'Publisher',
      iconFor: () => '<span class="category-mini-icon" aria-hidden="true">\u25E6</span>',
      countFor: publisher => sidebarCategoryCount(publisherCounts, publisher),
      empty: 'No publisher values found yet.',
      defaultCollapsed: false
    });
  } else if (structure === 'decade') {
    const decadeCounts = buildSidebarCategoryCountMap(state.items, decadeLabelForItem);
    const decades = [...new Set(state.items.map(decadeLabelForItem))].sort((a,b) => {
      if (a === 'Unknown Decade') return 1;
      if (b === 'Unknown Decade') return -1;
      return Number.parseInt(a,10) - Number.parseInt(b,10);
    });
    markup = categoryGroupMarkup('Decade', 'Decades', state.items, decades, {
      groupKind: 'Decade',
      iconFor: () => '<span class="category-mini-icon" aria-hidden="true">\u25F7</span>',
      countFor: decade => sidebarCategoryCount(decadeCounts, decade),
      empty: 'No dated entries found yet.',
      defaultCollapsed: false
    });
  } else {
    const config = sidebarCategoryModeConfig(structure);
    if (config) {
      const items = config.kind ? state.items.filter(i => i.kind === config.kind) : state.items;
      const counts = buildSidebarCategoryCountMap(items, item => sidebarCategoryValuesForItem(item, structure));
      const categories = sortCategoriesForKind(config.kind || 'Any', [...new Set(items.flatMap(item => sidebarCategoryValuesForItem(item, structure)))]);
      markup = categoryGroupMarkup(structure, config.label, items, categories, {
        groupKind: structure,
        iconFor: config.iconFor,
        countFor: category => sidebarCategoryCount(counts, category),
        empty: config.empty,
        defaultCollapsed: false
      });
    }
  }
  host.innerHTML = markup || '<p class="sub small-pad">Scan a library root to build categories.</p>';
  host.dataset.categoryCacheKey = cacheKey;
  guidevaultCategoryRenderKey = cacheKey;
}


function shouldVirtualizeLibraryGrid(list) {
  return Array.isArray(list) && list.length >= GUIDEVAULT_GRID_VIRTUAL_THRESHOLD;
}

function estimateGridColumns(host) {
  if (!host) return 1;
  const computed = window.getComputedStyle(host);
  const columns = String(computed.gridTemplateColumns || '').split(' ').filter(Boolean).length;
  if (columns > 0) return columns;
  const min = Math.max(120, Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--gv-card-min')) || 150);
  const gap = Number.parseFloat(computed.columnGap || computed.gap || '16') || 16;
  return Math.max(1, Math.floor((host.clientWidth + gap) / (min + gap)));
}

function measureVirtualGrid(host, current = {}) {
  const computed = window.getComputedStyle(host);
  const rowGap = Number.parseFloat(computed.rowGap || computed.gap || '16') || 16;
  const card = host.querySelector('.card');
  const cardHeight = card?.getBoundingClientRect?.().height || Number(current.cardHeight || 300) || 300;
  return {
    columns: estimateGridColumns(host),
    rowHeight: Math.max(160, Math.ceil(cardHeight + rowGap)),
    cardHeight: Math.max(120, Math.ceil(cardHeight))
  };
}

function virtualGridWindowFor(vgrid, force = false) {
  const scroller = libraryScrollElement();
  const host = vgrid?.host;
  const list = vgrid?.items || [];
  if (!scroller || !host || !list.length) return { startIndex: 0, endIndex: Math.min(list.length, GUIDEVAULT_GRID_INITIAL_RENDER), topHeight: 0, bottomHeight: 0 };
  const columns = Math.max(1, Number(vgrid.columns || 1));
  const rowHeight = Math.max(160, Number(vgrid.rowHeight || 300));
  const hostTop = scrollTopForElementWithinScroller(host, scroller);
  vgrid.hostTop = hostTop;
  const topWithinGrid = Math.max(0, scroller.scrollTop - hostTop - 12);
  const totalRows = Math.ceil(list.length / columns);
  const visibleFirstRow = Math.max(0, Math.floor(topWithinGrid / rowHeight));
  const visibleRowCount = Math.ceil((scroller.clientHeight || window.innerHeight || 800) / rowHeight);
  const visibleLastRow = Math.min(totalRows - 1, visibleFirstRow + visibleRowCount);

  if (!force && Number(vgrid.startIndex) >= 0 && Number(vgrid.endIndex) > Number(vgrid.startIndex)) {
    const renderedFirstRow = Math.floor(Number(vgrid.startIndex || 0) / columns);
    const renderedEndRowExclusive = Math.ceil(Number(vgrid.endIndex || 0) / columns);
    const guardRows = 1;
    const safeFirstRow = renderedFirstRow === 0 ? 0 : renderedFirstRow + guardRows;
    const safeLastRow = renderedEndRowExclusive >= totalRows ? totalRows - 1 : renderedEndRowExclusive - guardRows - 1;
    if (visibleFirstRow >= safeFirstRow && visibleLastRow <= safeLastRow) {
      return {
        startIndex: vgrid.startIndex,
        endIndex: vgrid.endIndex,
        topHeight: renderedFirstRow * rowHeight,
        bottomHeight: Math.max(0, totalRows - renderedEndRowExclusive) * rowHeight,
        reused: true
      };
    }
  }

  const firstRow = Math.max(0, visibleFirstRow - GUIDEVAULT_GRID_VIRTUAL_BUFFER_ROWS);
  const visibleRows = visibleRowCount + GUIDEVAULT_GRID_VIRTUAL_BUFFER_ROWS * 2;
  const maxRowsByCards = Math.max(visibleRows, Math.floor(GUIDEVAULT_GRID_VIRTUAL_MAX_CARDS / columns));
  const startIndex = Math.min(list.length, firstRow * columns);
  const endRow = Math.ceil(startIndex / columns) + maxRowsByCards;
  const endIndex = Math.min(list.length, Math.max(startIndex + columns, endRow * columns));
  const renderedRows = Math.ceil((endIndex - startIndex) / columns);
  const topHeight = firstRow * rowHeight;
  const bottomRows = Math.max(0, totalRows - firstRow - renderedRows);
  const bottomHeight = bottomRows * rowHeight;
  return { startIndex, endIndex, topHeight, bottomHeight };
}

function renderVirtualGridWindow(force = false) {
  const vgrid = state.virtualGrid;
  if (!vgrid?.host || !vgrid.host.isConnected) return;
  const host = vgrid.host;
  let columnsChanged = false;
  if (force || !Number(vgrid.columns) || !Number(vgrid.rowHeight)) {
    const measured = measureVirtualGrid(host, vgrid);
    columnsChanged = measured.columns !== vgrid.columns || Math.abs(measured.rowHeight - Number(vgrid.rowHeight || 0)) > 6;
    vgrid.columns = measured.columns;
    vgrid.rowHeight = measured.rowHeight;
    vgrid.cardHeight = measured.cardHeight;
  }
  const win = virtualGridWindowFor(vgrid, force || columnsChanged);
  if (!force && !columnsChanged && win.startIndex === vgrid.startIndex && win.endIndex === vgrid.endIndex) return;
  vgrid.startIndex = win.startIndex;
  vgrid.endIndex = win.endIndex;
  const slice = vgrid.items.slice(win.startIndex, win.endIndex);
  host.innerHTML = `${win.topHeight > 0 ? `<div class="gv-virtual-spacer gv-virtual-spacer-top" style="height:${Math.round(win.topHeight)}px"></div>` : ''}${slice.map(item => cardMarkupForItem(item)).join('')}${win.bottomHeight > 0 ? `<div class="gv-virtual-spacer gv-virtual-spacer-bottom" style="height:${Math.round(win.bottomHeight)}px"></div>` : ''}`;
  initializeCoverImages(host);
  attachCoverPrimeScrollHandler();
}

function attachVirtualGridHandlers() {
  const scroller = libraryScrollElement();
  if (scroller && scroller.dataset.virtualGridScroll !== '1') {
    scroller.dataset.virtualGridScroll = '1';
    let queued = false;
    scroller.addEventListener('scroll', () => {
      if (!state.virtualGrid || queued) return;
      queued = true;
      requestAnimationFrame(() => {
        queued = false;
        renderVirtualGridWindow(false);
      });
    }, { passive: true });
  }
  if (!guidevaultVirtualResizeAttached) {
    guidevaultVirtualResizeAttached = true;
    window.addEventListener('resize', () => {
      if (!state.virtualGrid) return;
      requestAnimationFrame(() => renderVirtualGridWindow(true));
    }, { passive: true });
  }
}

function renderVirtualGrid(id, list) {
  const host = $(id);
  if (!host) return;
  state.libraryRenderToken = (Number(state.libraryRenderToken || 0) + 1) % 1000000;
  state.virtualGrid = {
    id,
    host,
    items: list,
    columns: estimateGridColumns(host),
    rowHeight: 300,
    cardHeight: 280,
    hostTop: scrollTopForElementWithinScroller(host, libraryScrollElement()),
    startIndex: -1,
    endIndex: -1
  };
  host.classList.remove('stable-grid');
  host.classList.add('virtualized-grid');
  host.innerHTML = list.slice(0, Math.min(list.length, GUIDEVAULT_GRID_INITIAL_RENDER)).map(item => cardMarkupForItem(item)).join('');
  initializeCoverImages(host);
  attachCoverPrimeScrollHandler();
  attachVirtualGridHandlers();
  requestAnimationFrame(() => renderVirtualGridWindow(true));
}

function clearVirtualGridIfHost(id) {
  if (state.virtualGrid?.id === id) state.virtualGrid = null;
  const host = $(id);
  if (host) host.classList.remove('virtualized-grid');
}

function renderGrid(id, items) {
  const host = $(id);
  if (!host) return;
  const list = Array.isArray(items) ? items : [];
  if (!list.length) {
    clearVirtualGridIfHost(id);
    host.classList.remove('stable-grid');
    host.innerHTML = `<div class="empty-message">No content found. Set a Library Root folder in Settings and scan for CBZ, CBR, or PDF files.</div>`;
    return;
  }

  if (shouldVirtualizeLibraryGrid(list)) {
    renderVirtualGrid(id, list);
    return;
  }

  clearVirtualGridIfHost(id);
  host.classList.add('stable-grid');
  state.libraryRenderToken = (Number(state.libraryRenderToken || 0) + 1) % 1000000;
  const token = state.libraryRenderToken;
  const firstCount = Math.min(list.length, GUIDEVAULT_GRID_INITIAL_RENDER);
  host.innerHTML = list.slice(0, firstCount).map(item => cardMarkupForItem(item)).join('');
  initializeCoverImages(host);
  attachCoverPrimeScrollHandler();

  if (firstCount >= list.length) return;

  const more = document.createElement('div');
  more.className = 'library-progressive-render-note';
  more.textContent = `Loading remaining items... ${firstCount}/${list.length}`;
  host.appendChild(more);

  let index = firstCount;
  const appendChunk = () => {
    if (state.libraryRenderToken !== token || !host.isConnected) return;
    const end = Math.min(index + GUIDEVAULT_GRID_CHUNK_SIZE, list.length);
    const wrapper = document.createElement('template');
    wrapper.innerHTML = list.slice(index, end).map(item => cardMarkupForItem(item)).join('');
    const nodes = Array.from(wrapper.content.children);
    nodes.forEach(node => host.insertBefore(node, more));
    initializeCoverImages(host);
    index = end;
    if (index < list.length) {
      more.textContent = `Loading remaining items... ${index}/${list.length}`;
      if (typeof requestIdleCallback === 'function') requestIdleCallback(appendChunk, { timeout: 250 });
      else window.setTimeout(appendChunk, 16);
    } else {
      more.remove();
    }
  };
  if (typeof requestIdleCallback === 'function') requestIdleCallback(appendChunk, { timeout: 250 });
  else window.setTimeout(appendChunk, 16);
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
  { id: 'page-right', title: 'Page right', description: 'Move one page to the right', keys: ['\u2192'] },
  { id: 'page-left', title: 'Page left', description: 'Move one page to the left', keys: ['\u2190'] },
  { id: 'page-up', title: 'Page up', description: 'Move one page upwards', keys: ['\u2191'] },
  { id: 'page-down', title: 'Page down', description: 'Move one page downwards', keys: ['\u2193'] },
  { id: 'offset-double-page', title: 'Offset double page', description: 'Offset pages for double page spread alignment', keys: ['O'] },
  { id: 'first-page', title: 'First Page', description: 'Move to the first page', keys: ['Ctrl + \u2190'] },
  { id: 'last-page', title: 'Last Page', description: 'Move to the last page', keys: ['Ctrl + \u2192'] }
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
  list.innerHTML = KEYBIND_DEFAULTS.map((def) => {
    const keys = bindings[def.id]?.keys?.length ? bindings[def.id].keys : def.keys;
    return `<div class="keybind-row" data-keybind-id="${escapeForAttribute(def.id)}">
      <div class="keybind-main">
        <div class="keybind-title-line"><h3>${escapeHtml(def.title)}</h3><span>${keys.length} binding${keys.length === 1 ? '' : 's'}</span></div>
        <div class="keybind-chip-line">${keys.map(key => `<span class="keybind-key-chip">${escapeHtml(key)}</span>`).join('')}</div>
        <p class="sub">${escapeHtml(def.description)}</p>
      </div>
      <div class="keybind-actions" aria-label="${escapeForAttribute(def.title)} actions">
        <button class="keybind-action" data-keybind-action="add" type="button" title="Add alternate key" aria-label="Add alternate key">+</button>
        <button class="keybind-action" data-keybind-action="reset" type="button" title="Reset binding" aria-label="Reset binding">\u267B</button>
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
    .replace(/arrowright/ig, '\u2192')
    .replace(/arrowleft/ig, '\u2190')
    .replace(/arrowup/ig, '\u2191')
    .replace(/arrowdown/ig, '\u2193')
    .replace(/control/ig, 'ctrl')
    .replace(/command|cmd/ig, 'meta')
    .replace(/\s*\+\s*/g, '+')
    .replace(/\s+/g, '')
    .toLowerCase();
}

function keyEventToBindingLabel(e) {
  let key = e.key || '';
  if (key === ' ') key = 'Space';
  if (key === 'ArrowRight') key = '\u2192';
  else if (key === 'ArrowLeft') key = '\u2190';
  else if (key === 'ArrowUp') key = '\u2191';
  else if (key === 'ArrowDown') key = '\u2193';
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
function customizeSettingsHasUserContent(settings = {}) {
  const normalized = normalizeCustomize(settings || {});
  const shelves = normalized.homeShelves || [];
  const defaultShelves = shelves.length === 1 && shelves[0] === 'recently-added';
  const customItems = normalized.sideNav?.customItems || [];
  return !defaultShelves || customItems.length > 0;
}
function loadCustomizeSettings() {
  let parsed = {};
  try { parsed = JSON.parse(localStorage.getItem(GUIDEVAULT_CUSTOMIZE_KEY) || '{}') || {}; } catch {}
  state.customize = normalizeCustomize(parsed);
  return state.customize;
}
function persistCustomizeSettingsLocal() {
  state.customize = normalizeCustomize(state.customize || {});
  try { localStorage.setItem(GUIDEVAULT_CUSTOMIZE_KEY, JSON.stringify(state.customize)); } catch {}
  return state.customize;
}
async function saveCustomizeSettingsToServer(settings = state.customize) {
  try {
    await fetch('/api/customize/settings', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(normalizeCustomize(settings || {}))
    });
  } catch (err) {
    console.warn('Customize settings could not be saved to the server.', err);
  }
}
function saveCustomizeSettings() {
  const saved = persistCustomizeSettingsLocal();
  if (guidevaultCustomizeSaveTimer) window.clearTimeout(guidevaultCustomizeSaveTimer);
  guidevaultCustomizeSaveTimer = window.setTimeout(() => {
    guidevaultCustomizeSaveTimer = null;
    saveCustomizeSettingsToServer(saved);
  }, 160);
}
async function syncCustomizeSettingsFromServer(renderAfter = false) {
  if (guidevaultCustomizeSyncInFlight) return state.customize || loadCustomizeSettings();
  guidevaultCustomizeSyncInFlight = true;
  const local = state.customize || loadCustomizeSettings();
  try {
    const res = await fetch(`/api/customize/settings?_=${Date.now()}`, { cache: 'no-store' });
    if (!res.ok) throw new Error(await res.text());
    const server = normalizeCustomize(await res.json());
    const localHasContent = customizeSettingsHasUserContent(local);
    const serverHasContent = customizeSettingsHasUserContent(server);
    state.customize = serverHasContent || !localHasContent ? server : normalizeCustomize(local);
    persistCustomizeSettingsLocal();
    if (localHasContent && !serverHasContent) saveCustomizeSettingsToServer(state.customize);
    if (renderAfter) {
      renderCustomSideNavItems();
      if (!$('settingsCustomizePanel')?.classList.contains('hidden')) renderCustomizeSettings();
      updateNavActive();
    }
  } catch (err) {
    console.warn('Customize settings could not be loaded from the server; using local browser settings.', err);
  } finally {
    guidevaultCustomizeSyncInFlight = false;
  }
  return state.customize;
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
  if (kindScope === 'Strategy Guide') return '\u25A4';
  if (kindScope === 'Magazine') return '\u25A7';
  if (kindScope === 'Manual') return '\u25A6';
  return ({ series: '\u25A6', kind: '\u25A4', category: '\u2318', publisher: '\u25EB', list: '\u2637', search: '\u2315' })[type] || '\u2637';
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
  if ($('customSideNavIconPreset')) $('customSideNavIconPreset').value = '\u2637';
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
  setDefaultSortForCurrentLibraryView();
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
    list.innerHTML = (settings.homeShelves || []).map((id) => {
      const opt = HOME_SHELF_OPTIONS.find(o => o.id === id) || HOME_SHELF_OPTIONS[0];
      return `<div class="customize-shelf-row" data-shelf-id="${escapeForAttribute(id)}">
        <span class="customize-shelf-handle" draggable="true" role="button" aria-label="Drag ${escapeForAttribute(opt.label)} shelf" title="Drag to reorder">\u283F</span>
        <div class="customize-shelf-copy"><strong>${escapeHtml(opt.label)}</strong><p class="sub">${escapeHtml(opt.description)}</p><span class="customize-shelf-pill">Visible on Home</span></div>
        <div class="customize-shelf-actions">
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
  state.customize.homeShelves = shelves;
  saveCustomizeSettings();
  renderCustomizeSettings();
  render();
  setCustomizeStatus(action === 'remove' ? 'Home shelf removed.' : 'Home shelf layout updated.', 'success');
}
let customizeShelfDragId = '';
function clearCustomizeShelfDragState() {
  document.querySelectorAll('.customize-shelf-row.dragging,.customize-shelf-row.drag-over-before,.customize-shelf-row.drag-over-after').forEach(row => {
    row.classList.remove('dragging', 'drag-over-before', 'drag-over-after');
  });
}
function handleCustomizeShelfDragStart(e) {
  const handle = e.target.closest?.('.customize-shelf-handle');
  const row = handle?.closest?.('.customize-shelf-row');
  if (!handle || !row) return;
  customizeShelfDragId = row.dataset.shelfId || '';
  if (!customizeShelfDragId) { e.preventDefault(); return; }
  row.classList.add('dragging');
  if (e.dataTransfer) {
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', customizeShelfDragId);
  }
  setCustomizeStatus('Drag the shelf to a new position, then release.', 'info');
}
function handleCustomizeShelfDragOver(e) {
  if (!customizeShelfDragId) return;
  const row = e.target.closest?.('.customize-shelf-row');
  if (!row || row.dataset.shelfId === customizeShelfDragId) return;
  e.preventDefault();
  if (e.dataTransfer) e.dataTransfer.dropEffect = 'move';
  document.querySelectorAll('.customize-shelf-row.drag-over-before,.customize-shelf-row.drag-over-after').forEach(el => {
    if (el !== row) el.classList.remove('drag-over-before', 'drag-over-after');
  });
  const rect = row.getBoundingClientRect();
  const insertBefore = e.clientY < rect.top + rect.height / 2;
  row.classList.toggle('drag-over-before', insertBefore);
  row.classList.toggle('drag-over-after', !insertBefore);
}
function handleCustomizeShelfDrop(e) {
  if (!customizeShelfDragId) return;
  const row = e.target.closest?.('.customize-shelf-row');
  if (!row) return;
  e.preventDefault();
  const sourceId = e.dataTransfer?.getData('text/plain') || customizeShelfDragId;
  const targetId = row.dataset.shelfId || '';
  if (!sourceId || !targetId || sourceId === targetId) { clearCustomizeShelfDragState(); customizeShelfDragId = ''; return; }
  const settings = state.customize || loadCustomizeSettings();
  const shelves = (settings.homeShelves || []).slice();
  const sourceIndex = shelves.indexOf(sourceId);
  let targetIndex = shelves.indexOf(targetId);
  if (sourceIndex < 0 || targetIndex < 0) { clearCustomizeShelfDragState(); customizeShelfDragId = ''; return; }
  const [moved] = shelves.splice(sourceIndex, 1);
  if (sourceIndex < targetIndex) targetIndex -= 1;
  const rect = row.getBoundingClientRect();
  const insertAfter = e.clientY >= rect.top + rect.height / 2;
  shelves.splice(targetIndex + (insertAfter ? 1 : 0), 0, moved);
  state.customize.homeShelves = shelves;
  clearCustomizeShelfDragState();
  customizeShelfDragId = '';
  saveCustomizeSettings();
  renderCustomizeSettings();
  render();
  setCustomizeStatus('Home shelf order updated.', 'success');
}
function handleCustomizeShelfDragEnd() {
  clearCustomizeShelfDragState();
  customizeShelfDragId = '';
}

function readReadingActivity() {
  try { return JSON.parse(localStorage.getItem(GUIDEVAULT_READING_ACTIVITY_KEY) || '[]').filter(Boolean); } catch { return []; }
}
function saveReadingActivity(events) {
  const clean = Array.isArray(events) ? events.slice(-GUIDEVAULT_READING_ACTIVITY_LIMIT) : [];
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
    pageCount: Number(item.pageCount || item.pages || item.PageCount || 0) || 0,
    user: profile.username || profile.email || 'local user',
    action,
    at: new Date().toISOString()
  });
  saveReadingActivity(events);
}
function shelfItemsFor(id, items = state.items, limit = HOME_SHELF_MAX_ITEMS) {
  const all = Array.isArray(items) ? items : [];
  const cacheable = id !== 'recently-viewed';
  const cacheKey = `${Number(state.libraryCategoryCacheVersion || 0)}|${id}|${limit}|${all.length}`;
  if (cacheable && guidevaultHomeShelfCacheKey !== `${Number(state.libraryCategoryCacheVersion || 0)}|${all.length}`) {
    guidevaultHomeShelfCacheKey = `${Number(state.libraryCategoryCacheVersion || 0)}|${all.length}`;
    guidevaultHomeShelfCache = new Map();
  }
  if (cacheable && guidevaultHomeShelfCache.has(cacheKey)) return guidevaultHomeShelfCache.get(cacheKey);

  const byRecent = (a,b) => (itemRecentTimestamp(b) - itemRecentTimestamp(a)) || compareTextForSort(displayTitle(a), displayTitle(b));
  const byTitle = (a,b) => compareTextForSort(displayTitle(a), displayTitle(b));
  const byLargest = (a,b) => (Number(b.sizeBytes||b.SizeBytes||0)-Number(a.sizeBytes||a.SizeBytes||0)) || byTitle(a,b);
  let result = [];
  if (id === 'recently-added') result = takeBestLibraryItems(all, limit, byRecent);
  else if (id === 'manuals') result = takeBestLibraryItems(all, limit, byRecent, i => i.kind === 'Manual');
  else if (id === 'strategy-guides') result = takeBestLibraryItems(all, limit, byRecent, i => i.kind === 'Strategy Guide');
  else if (id === 'magazines') result = takeBestLibraryItems(all, limit, byRecent, i => i.kind === 'Magazine');
  else if (id === 'unsorted-strategy-guides') result = takeBestLibraryItems(all, limit, byTitle, i => i.kind === 'Strategy Guide' && isBlankish(preferredPlatformOf(i)));
  else if (id === 'multi-platform-guides') result = takeBestLibraryItems(all, limit, byTitle, i => i.kind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(i));
  else if (id === 'largest-files') result = takeBestLibraryItems(all, limit, byLargest);
  else if (id === 'recently-viewed') {
    const lookup = new Map(all.map(item => [String(item.id || item.Id), item]));
    const seenIds = readReadingActivity().slice().reverse().map(e => e.id).filter(Boolean);
    const unique = [...new Set(seenIds)];
    result = unique.map(itemId => lookup.get(String(itemId))).filter(Boolean).slice(0, limit);
  }
  if (cacheable) guidevaultHomeShelfCache.set(cacheKey, result);
  return result;
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
        ${hasPages ? `<div class="home-shelf-controls" aria-label="${escapeForAttribute(opt.label)} shelf navigation"><span>${escapeHtml(rangeText)}</span><button class="home-shelf-arrow" data-home-shelf-nav="prev" data-home-shelf-id="${escapeForAttribute(id)}" type="button" ${offset <= 0 ? 'disabled' : ''} aria-label="Previous ${escapeForAttribute(opt.label)} items">\u2039</button><button class="home-shelf-arrow" data-home-shelf-nav="next" data-home-shelf-id="${escapeForAttribute(id)}" type="button" ${offset >= maxOffset ? 'disabled' : ''} aria-label="Next ${escapeForAttribute(opt.label)} items">\u203A</button></div>` : ''}
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
  const computed = libraryItemComputed(item);
  const title = computed.title || displayTitle(item);
  return `<article class="card ${specialCardClass(item)} ${state.selected?.id === item.id ? 'selected' : ''}" data-id="${escapeForAttribute(itemId)}" data-alpha="${computed.alpha || alphaKey(title)}">
      <button class="favorite${favorite ? ' active' : ''}" type="button" data-id="${escapeForAttribute(itemId)}" aria-label="${favorite ? 'Remove from favorites' : 'Add to favorites'}" aria-pressed="${favorite ? 'true' : 'false'}" title="${favorite ? 'Remove from favorites' : 'Add to favorites'}">\u2605</button>
      <div class="cover-wrap"><img decoding="async" loading="lazy" fetchpriority="low" data-cover-src="${escapeForAttribute(cover)}" src="${GUIDEVAULT_TRANSPARENT_COVER_PLACEHOLDER}" alt="${escapeForAttribute(title)} cover" /></div>
      <div class="card-body">
        <div class="card-title">${escapeHtml(title)}</div>
        ${libraryCardPlatformMetaHtml(item)}
        <small>${escapeHtml(item.year)}</small>
        <div class="badge-line"><span class="format ${kindClass(item.kind)}">${escapeHtml(item.kind)}</span><span class="pill">${itemPageCountLabel(item)}</span></div>
      </div>
    </article>`;
}
function dateValue(value) {
  if (value instanceof Date) {
    const t = value.getTime();
    return Number.isFinite(t) ? t : 0;
  }
  if (typeof value === 'number') return Number.isFinite(value) ? value : 0;
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
  const effectiveItem = applyClientMetadataOverride(item);
  cleanupInactiveViewsForNavigation('detail');
  document.body.classList.remove('settings-sidebar-mode', 'reader-page-mode', 'profile-page-mode');
  document.body.classList.add('detail-page-mode');
  document.body.classList.remove('right-collapsed');
  state.selected = effectiveItem;
  resetIndividualMetadataLookupsForItem(effectiveItem);
  recordReadingActivity(effectiveItem, 'view');
  hideAppView('libraryView');
  hideAppView('settingsView');
  hideAppView('profileView');
  hideAppView('readerView');
  showAppView('detailView');
  activateTab('overview');
  renderDetails(effectiveItem);
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
        setDetailNavigationContextFromCard(card, item);
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
    setDetailNavigationContextFromCard(card, item);
    showDetailScreen(item);
  }, true);
}




function isBlankish(value) {
  const v = String(value || '').trim();
  return !v || v === '\u2014' || /^unknown$/i.test(v) || /^unsorted$/i.test(v);
}
function detectedSystemOf(item) {
  if (!item) return '\u2014';
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
  return '\u2014';
}

function platformListHtml(item) {
  const platforms = associatedPlatformsOf(item);
  if (!platforms.length) return '\u2014';
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
  const rendered = isHtml ? String(value || '') : escapeHtml(String(value || '\u2014'));
  return `<dt>${escapeHtml(label)}</dt><dd>${rendered}</dd>`;
}

function itemArray(value) {
  if (Array.isArray(value)) return value.map(v => String(v || '').trim()).filter(Boolean);
  if (typeof value === 'string') return value.split(/[;,|]/).map(v => v.trim()).filter(Boolean);
  return [];
}

function itemList(value) {
  const values = itemArray(value);
  return values.length ? values.join(', ') : '\u2014';
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


const METADATA_LOCKABLE_FIELDS = {
  editTitle: 'title',
  editCategory: 'preferredPlatform',
  editEsrbRating: 'rating',
  editAssociatedPlatforms: 'associatedPlatforms',
  editPlatformMatchTitle: 'platformMatchTitle',
  editGameTitle: 'gameTitle',
  editManualType: 'manualType',
  editIsbn: 'isbn',
  editGuideType: 'guideType',
  editEditionType: 'edition',
  editEditionYear: 'editionYear',
  editEditionVolume: 'editionVolume',
  editFranchise: 'franchise',
  editSeries: 'series',
  editMagazineTitle: 'magazineTitle',
  editIssue: 'issueNumber',
  editVolume: 'volume',
  editCoverDate: 'coverDate',
  editBarcodeUpcIssn: 'barcodeUpcIssn',
  editPublicationDate: 'publicationDate',
  editPublisher: 'publisher',
  editRegion: 'region',
  editLanguageTag: 'languageTag',
  editPlatformFocus: 'platformFocus',
  editPrimarySystem: 'primarySystem',
  editMagazineCategory: 'magazineCategory',
  editCoverSubject: 'coverSubject',
  editYear: 'year',
  editPageCount: 'pageCount',
  editPublicationDateGuide: 'publicationDate',
  editWriter: 'writer',
  editDeveloper: 'developer',
  editGamePublisher: 'gamePublisher',
  editGameReleaseYear: 'gameReleaseYear',
  editGenre: 'genre',
  editSummary: 'summary',
  editFeaturedGames: 'featuredGames',
  editFeaturedPlatforms: 'featuredPlatforms',
  editSpecialFeatures: 'specialFeatures',
  editIncludedExtras: 'includedExtras',
  editIncludedSections: 'includedSections',
  editControlScheme: 'controlScheme',
  editItemsCovered: 'itemsCovered',
  editWarrantySupport: 'warrantySupport',
  editCoveredGames: 'coveredGames',
  editCoveredPlatforms: 'coveredPlatforms',
  editGuideTopics: 'guideTopics',
  editStrategySpecialFeatures: 'specialFeatures',
  editCharactersCovered: 'charactersCovered',
  editLocationsCovered: 'locationsCovered',
  editTags: 'tags'
};

const METADATA_PAYLOAD_LOCK_ALIASES = {
  title: ['title', 'strategyGuideTitle', 'name'],
  strategyGuideTitle: ['title', 'strategyGuideTitle'],
  manualTitle: ['manualTitle'],
  magazineTitle: ['magazineTitle'],
  name: ['title', 'name'],
  category: ['preferredPlatform', 'category', 'system'],
  system: ['preferredPlatform', 'system', 'category'],
  preferredPlatform: ['preferredPlatform', 'category', 'system'],
  primarySystem: ['primarySystem'],
  language: ['languageTag', 'language'],
  languageTag: ['languageTag', 'language'],
  rating: ['rating', 'esrb'],
  esrb: ['rating', 'esrb'],
  writer: ['writer', 'authorWriter', 'editor'],
  authorWriter: ['writer', 'authorWriter'],
  publishYear: ['year', 'publishYear'],
  year: ['year', 'publishYear'],
  isbn: ['isbn', 'isbn10', 'isbn13'],
  isbn10: ['isbn10', 'isbn'],
  isbn13: ['isbn13', 'isbn'],
  barcode: ['barcodeUpcIssn', 'barcode'],
  upc: ['barcodeUpcIssn', 'upc'],
  issn: ['barcodeUpcIssn', 'issn'],
  barcodeUpcIssn: ['barcodeUpcIssn', 'barcode', 'upc', 'issn'],
  pageCount: ['pageCount', 'metadataPageCount'],
  metadataPageCount: ['pageCount', 'metadataPageCount'],
  gameDeveloper: ['developer', 'gameDeveloper'],
  developer: ['developer', 'gameDeveloper'],
  series: ['series'],
  gameFranchise: ['franchise', 'gameFranchise'],
  franchise: ['franchise', 'gameFranchise'],
  editionType: ['edition', 'editionType'],
  edition: ['edition', 'editionType'],
  physicalExtras: ['includedExtras', 'physicalExtras'],
  includedExtras: ['includedExtras', 'physicalExtras'],
  coverStory: ['coverSubject', 'coverStory'],
  coverSubject: ['coverSubject', 'coverStory'],
  description: ['summary', 'description'],
  summary: ['summary', 'description']
};

function normalizeMetadataLocks(locks = {}) {
  const normalized = {};
  Object.entries(locks || {}).forEach(([key, value]) => {
    const lockKey = String(key || '').trim();
    if (!lockKey) return;
    if (value === true) {
      normalized[lockKey] = { locked: true, lockedAt: new Date().toISOString(), source: 'manual' };
      return;
    }
    if (!value || value === false) return;
    const entry = { ...(typeof value === 'object' ? value : {}) };
    if (entry.locked === false) return;
    normalized[lockKey] = {
      locked: true,
      lockedAt: entry.lockedAt || new Date().toISOString(),
      lockedBy: entry.lockedBy || '',
      reason: entry.reason || '',
      source: entry.source || 'manual'
    };
  });
  return normalized;
}

function metadataLocksOf(item = state.selected || {}) {
  return normalizeMetadataLocks(item?.metadataLocks || item?.MetadataLocks || {});
}

function metadataLockKeysForPayloadKey(key = '') {
  const normalized = String(key || '').trim();
  return METADATA_PAYLOAD_LOCK_ALIASES[normalized] || [normalized];
}

function isMetadataFieldLocked(item = state.selected || {}, key = '') {
  const locks = metadataLocksOf(item);
  return metadataLockKeysForPayloadKey(key).some(lockKey => locks[lockKey]?.locked !== false && !!locks[lockKey]);
}

function metadataPayloadShouldRespectLocks(payload = {}) {
  const source = String(payload?.metadataSource || '').trim();
  return !!source && !/manual edit/i.test(source) && payload?.overwriteLockedFields !== true;
}

function filterLockedMetadataPayload(item, payload = {}) {
  if (!metadataPayloadShouldRespectLocks(payload)) return { payload, skipped: [] };
  const filtered = {};
  const skipped = [];
  const alwaysKeep = new Set(['metadataSource','metadataStatus','metadataLocks','overwriteLockedFields','igdbId','igdbUrl','esrbId','esrbUrl','sourceUrl','savedAt']);
  Object.entries(payload || {}).forEach(([key, value]) => {
    if (alwaysKeep.has(key)) { filtered[key] = value; return; }
    const keys = metadataLockKeysForPayloadKey(key);
    const locked = keys.some(lockKey => isMetadataFieldLocked(item, lockKey));
    if (locked) { skipped.push(key); return; }
    filtered[key] = value;
  });
  return { payload: filtered, skipped };
}

function currentMetadataLocksPayload() {
  return metadataLocksOf(state.selected || {});
}

function rememberMetadataControlTitle(el) {
  if (!el || el.dataset.metadataLockOriginalTitle !== undefined) return;
  el.dataset.metadataLockOriginalTitle = el.getAttribute('title') || '';
}

function restoreMetadataControlTitle(el) {
  if (!el || el.dataset.metadataLockOriginalTitle === undefined) return;
  const original = el.dataset.metadataLockOriginalTitle || '';
  if (original) el.setAttribute('title', original);
  else el.removeAttribute('title');
  delete el.dataset.metadataLockOriginalTitle;
}

function setMetadataFieldEditorLocked(fieldId = '', locked = false) {
  if (!fieldId) return;
  const control = $(fieldId);
  const shell = document.querySelector(`.meta-multi-select[data-multi-select="${CSS.escape(fieldId)}"]`);
  const lockedTitle = 'This field is locked. Unlock it before editing.';

  if (control) {
    const tag = String(control.tagName || '').toLowerCase();
    const usesDisabled = tag === 'select' || control.type === 'checkbox' || control.type === 'radio';
    control.classList.toggle('metadata-lock-readonly', locked);
    control.dataset.metadataLocked = locked ? 'true' : 'false';

    if (locked) {
      rememberMetadataControlTitle(control);
      if (usesDisabled) control.disabled = true;
      else control.readOnly = true;
      control.setAttribute('aria-readonly', 'true');
      control.title = lockedTitle;
    } else {
      restoreMetadataControlTitle(control);
      if (usesDisabled) control.disabled = false;
      else control.readOnly = control.classList.contains('metadata-derived-readonly');
      if (control.readOnly) control.setAttribute('aria-readonly', 'true');
      else control.removeAttribute('aria-readonly');
      delete control.dataset.metadataLocked;
    }
  }

  if (shell) {
    shell.classList.toggle('metadata-lock-readonly', locked);
    shell.dataset.metadataLocked = locked ? 'true' : 'false';
    if (locked) shell.classList.remove('open');
    const button = shell.querySelector('.meta-multi-button');
    if (button) {
      if (locked) {
        rememberMetadataControlTitle(button);
        button.disabled = true;
        button.setAttribute('aria-disabled', 'true');
        button.setAttribute('aria-expanded', 'false');
        button.title = lockedTitle;
      } else {
        restoreMetadataControlTitle(button);
        button.disabled = false;
        button.removeAttribute('aria-disabled');
      }
    }
    shell.querySelectorAll('.meta-multi-option-input').forEach(input => {
      input.disabled = locked;
    });
    if (!locked) delete shell.dataset.metadataLocked;
  }
}

function refreshMetadataLockButtons() {
  document.querySelectorAll('.metadata-lock-button[data-metadata-lock-key]').forEach(button => {
    const key = button.dataset.metadataLockKey || '';
    const locked = isMetadataFieldLocked(state.selected || {}, key);
    button.classList.toggle('locked', locked);
    button.textContent = locked ? '\uD83D\uDD12' : '\uD83D\uDD13';
    button.setAttribute('aria-pressed', locked ? 'true' : 'false');
    button.title = locked
      ? 'Locked: this field is read-only and scraper, import, batch, and normalize actions cannot overwrite it.'
      : 'Unlocked: this field can be edited and scraper, import, batch, and normalize actions may update it.';
    button.setAttribute('aria-label', `${locked ? 'Unlock' : 'Lock'} ${button.dataset.metadataLockLabel || 'field'}`);
    setMetadataFieldEditorLocked(button.dataset.metadataLockFor || '', locked);
  });
}

function addMetadataFieldLockButtons() {
  const panel = $('metadataPanel');
  if (!panel || panel.dataset.lockButtonsReady === 'true') { refreshMetadataLockButtons(); return; }
  const labels = [...panel.querySelectorAll('label')].filter(label => !label.closest('.meta-multi-panel'));
  labels.forEach(label => {
    if (label.querySelector(':scope > .metadata-lock-button')) return;
    const fieldId = metadataHelpFieldIdForLabel(label);
    const lockKey = METADATA_LOCKABLE_FIELDS[fieldId];
    if (!fieldId || !lockKey) return;
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'metadata-lock-button';
    button.dataset.metadataLockFor = fieldId;
    button.dataset.metadataLockKey = lockKey;
    button.dataset.metadataLockLabel = label.firstChild?.textContent?.trim() || 'field';
    const anchor = label.querySelector(':scope > input, :scope > select, :scope > textarea, :scope > .meta-multi-select') || null;
    label.insertBefore(button, anchor);
  });
  panel.dataset.lockButtonsReady = 'true';
  refreshMetadataLockButtons();
}

async function persistSelectedMetadataLocks(lockMap, serverLockPayload = null) {
  const selectedId = String(state.selected?.id || state.selected?.Id || '').trim();
  if (!selectedId) return;
  const normalized = normalizeMetadataLocks(lockMap);
  const locksForServer = serverLockPayload || normalized;
  const applyToItem = item => {
    if (!item) return item;
    item.metadataLocks = normalized;
    item.MetadataLocks = normalized;
    return item;
  };
  applyToItem(state.selected);
  const idx = (state.items || []).findIndex(i => String(i.id || i.Id || '') === selectedId);
  if (idx >= 0) { applyToItem(state.items[idx]); prepareLibraryItemComputedFields(state.items[idx]); markLibraryIndexesDirty(); }
  rememberClientMetadataOverride(selectedId, { metadataLocks: normalized });
  refreshMetadataLockButtons();
  try {
    const res = await fetch(`/api/items/${encodeURIComponent(selectedId)}/metadata`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ metadataLocks: locksForServer })
    });
    if (!res.ok) {
      let message = `HTTP ${res.status}`;
      try { message = (await res.json()).error || message; } catch {}
      throw new Error(message);
    }
    let updated = null;
    try { updated = await res.json(); } catch {}
    if (updated?.id || updated?.Id) {
      replaceItemInState(mergeSavedMetadataClientSide(state.selected, updated, { metadataLocks: normalized }));
      refreshMetadataLockButtons();
    }
    setStatus('Metadata field lock updated.');
  } catch (err) {
    console.error('Metadata lock save failed', err);
    setStatus(`Unable to save metadata lock: ${err?.message || err}`);
  }
}

function toggleSelectedMetadataLock(lockKey = '') {
  if (!state.selected || !lockKey) return;
  const locks = metadataLocksOf(state.selected);
  if (locks[lockKey]) {
    const serverPatch = { ...locks, [lockKey]: { locked: false, source: 'manual' } };
    delete locks[lockKey];
    persistSelectedMetadataLocks(locks, serverPatch);
    return;
  }
  locks[lockKey] = { locked: true, lockedAt: new Date().toISOString(), source: 'manual', reason: 'Manually locked in Guidevault' };
  persistSelectedMetadataLocks(locks);
}


function metadataPanelVisibleLockButtons() {
  return Array.from(document.querySelectorAll('#metadataPanel .metadata-lock-button[data-metadata-lock-key]')).filter(button => {
    const label = button.closest('label');
    if (!label) return false;
    if (label.classList.contains('hidden')) return false;
    if (label.closest('.hidden')) return false;
    try {
      const style = window.getComputedStyle(label);
      if (style.display === 'none' || style.visibility === 'hidden') return false;
    } catch {}
    return !!button.dataset.metadataLockKey;
  });
}

function selectedVisibleMetadataLockKeys() {
  return Array.from(new Set(metadataPanelVisibleLockButtons()
    .map(button => String(button.dataset.metadataLockKey || '').trim())
    .filter(Boolean)));
}

async function setAllSelectedMetadataFieldLocks(locked = true) {
  if (!state.selected) { setStatus('Select an item before changing field locks.'); return; }
  addMetadataFieldLockButtons();
  const keys = selectedVisibleMetadataLockKeys();
  if (!keys.length) { setStatus('No visible metadata fields are available to lock.'); return; }
  const current = metadataLocksOf(state.selected);
  const next = { ...current };
  const now = new Date().toISOString();
  if (locked) {
    keys.forEach(key => {
      next[key] = { locked: true, lockedAt: current[key]?.lockedAt || now, source: 'manual', reason: 'Locked by Lock All Fields in Guidevault' };
    });
    await persistSelectedMetadataLocks(next);
    setStatus(`Locked ${keys.length} visible metadata field${keys.length === 1 ? '' : 's'}.`);
    return;
  }
  const serverPatch = { ...current };
  keys.forEach(key => {
    if (serverPatch[key]) serverPatch[key] = { locked: false, source: 'manual' };
    delete next[key];
  });
  await persistSelectedMetadataLocks(next, serverPatch);
  setStatus(`Unlocked ${keys.length} visible metadata field${keys.length === 1 ? '' : 's'}.`);
}

const METADATA_FIELD_HELP = {
  editTitle: {
    default: 'Guidevault display title for this item. For manuals, use the manual-facing title; for strategy guides, use the guide title; for magazines, use the issue entry title.',
    Manual: 'Primary display title for the manual. For manuals, this serves as the manual title; use Game Title for the associated game.',
    'Strategy Guide': 'Strategy guide title printed on the guide or used as the Guidevault display title.',
    Magazine: 'Issue-level display title. Use the Magazine Title field for the publication name; use this only when the issue needs a distinct entry title.'
  },
  editKind: 'Content type. This controls which metadata fields are shown and how the item is grouped.',
  editMetadataStatus: 'Workflow state for this item. Use Reviewed when you have checked the metadata; use Manual Only when scraper results should no longer matter; use Failed Lookup when external lookup did not find a usable match.',
  editCategory: {
    default: 'Preferred platform used as the primary platform/category for this item. Strategy guides with multiple associated platforms display as Multi-Platform.',
    Magazine: 'Magazine grouping or publication shelf. Usually the publication name, such as Nintendo Power, EGM, or GamePro.'
  },
  editEsrbRating: 'ESRB rating for the associated game. This belongs to the game, not to the physical manual or guide.',
  editAssociatedPlatforms: 'Comma-separated platform list where this item should appear. Use Guidevault platform names when possible so icons match.',
  editPlatformMatchTitle: 'Detected title used by Guidevault when resolving platform or game metadata. Usually hidden after metadata cleanup.',
  editGameTitle: 'Title of the associated game. For manuals, this is the game the manual originally shipped with.',
  editManualType: 'Manual format or classification, such as Instruction Manual, Reference Card, Map, Controls Card, or Operations Manual.',
  editIsbn: 'ISBN for books/strategy guides. Prefer ISBN-10 when both ISBN-10 and ISBN-13 are known.',
  editGuideType: 'One or more guide classifications, such as Official Guide, Unofficial Guide, Hint Book, Code Book, or Full Walkthrough.',
  editEditionType: "Edition classification for a strategy guide, such as First Edition, Collector's Edition, Year Edition, or Volume Edition.",
  editEditionYear: 'Year value used only when the Edition Type includes Year Edition.',
  editEditionVolume: 'Volume number used only when the Edition Type includes Volume Edition.',
  editFranchise: 'Game franchise or series, such as Resident Evil, Final Fantasy, Zelda, or Sonic.',
  editSeries: 'Legacy series/grouping value. For magazines, use Magazine Title as the publication name instead.',
  editIssue: 'Magazine issue number.',
  editBarcodeUpcIssn: 'Barcode, UPC, or ISSN printed on the magazine issue when known. Use this for exact physical issue identification.',
  editMagazineTitle: 'Magazine publication title, such as Nintendo Power, EGM, GamePro, or Official PlayStation Magazine.',
  editVolume: 'Magazine volume value when printed or known.',
  editCoverDate: 'Date printed on the magazine cover. This can differ from actual publication date.',
  editPublicationDate: 'Actual magazine publication/release date when known. Use Cover Date for the printed cover date.',
  editRegion: {
    default: 'Region or market for the item, such as US, UK, Japan, Europe, or World.',
    Magazine: 'Country or market of publication for this magazine issue, such as US, UK, Japan, Europe, or World.'
  },
  editLanguageTag: 'Language value for the item. Prefer full words such as English, Japanese, or Spanish.',
  editPlatformFocus: 'Magazine audience or platform focus, such as Nintendo, Sega, PlayStation, PC, or Multi-platform. This is not the publication title.',
  editPrimarySystem: 'Primary system emphasized by this issue when one platform is more specific than the broader magazine focus.',
  editMagazineCategory: 'Magazine category, such as Official, PC gaming, cheats/codes, preview, or general gaming.',
  editCoverSubject: 'Main cover game, character, hardware, or feature subject.',
  editYear: {
    default: 'General item year. For books/guides this is usually the publication year; for magazines it is the issue year.',
    Manual: 'Manuals do not use the generic Year field. Use Game Release Year for the associated game instead.'
  },
  editPublicationDateGuide: 'Publication date for a strategy guide or book. Manuals normally do not use this field.',
  editPageCount: 'User-entered page count for the item. Use the real page count when known; archive page counts may be unreliable.',
  editPublisher: 'Publisher of the physical item. For manuals this may be the game publisher when printed or known.',
  editWriter: {
    default: 'Author or writer credit for a book, guide, or item when available.',
    Magazine: 'Editor or editor-in-chief credit for the magazine issue when known.'
  },
  editDeveloper: 'Developer of the associated game.',
  editGamePublisher: 'Publisher of the associated game, separate from the physical book/manual publisher when needed.',
  editGameReleaseYear: 'Release year of the associated game. For manuals, this is valid because the manual is tied to the game release.',
  editGenre: 'Game genre or broad content genre, such as RPG, Fighting, Action Adventure, Racing, or Puzzle.',
  editFeaturedGames: 'Comma-separated games featured in a magazine issue.',
  editFeaturedPlatforms: 'Comma-separated platforms featured in a magazine issue.',
  editSpecialFeatures: "Comma-separated recurring sections, departments, columns, or major magazine components, such as Mail Box, Player's Poll, Top 30, or Counselors Corner.",
  editIncludedExtras: {
    default: 'Physical extras included with the item, such as posters, maps, inserts, registration cards, discs, overlays, or stickers.',
    Magazine: 'Insert details for this magazine issue, such as posters, maps, trading cards, subscription inserts, discs, or removable extras.'
  },
  editIncludedSections: 'Comma-separated manual sections, such as Controls, Story, Characters, Items, Warranty, or Troubleshooting.',
  editControlScheme: 'Control method or scheme covered by the manual, such as controller, keyboard, mouse, light gun, or arcade controls.',
  editItemsCovered: 'Comma-separated items, mechanics, modes, or gameplay systems explained in the manual.',
  editWarrantySupport: 'Warranty, service, support, registration, or troubleshooting information printed in the manual.',
  editCoveredGames: 'Comma-separated games covered by a strategy guide.',
  editCoveredPlatforms: 'Comma-separated platforms covered by a strategy guide.',
  editGuideTopics: 'Comma-separated guide topics, such as walkthrough, maps, secrets, bosses, codes, endings, or collectibles.',
  editStrategySpecialFeatures: 'Comma-separated special features specific to the strategy guide.',
  editCharactersCovered: 'Comma-separated characters covered by the manual or guide.',
  editLocationsCovered: 'Comma-separated locations, maps, stages, worlds, or areas covered by the guide.',
  editSummary: 'Short description or summary for the item.',
  editTags: 'Comma-separated freeform tags for filtering and discovery.'
};

function metadataFieldHelpText(fieldId, kind = $('editKind')?.value || state.selected?.kind || '') {
  const entry = METADATA_FIELD_HELP[fieldId];
  if (!entry) return '';
  if (typeof entry === 'string') return entry;
  return entry[kind] || entry.default || '';
}

function metadataHelpFieldIdForLabel(label) {
  const multi = label.querySelector(':scope > .meta-multi-select[data-multi-select]');
  if (multi?.dataset?.multiSelect) return multi.dataset.multiSelect;
  const control = label.querySelector(':scope > input[id], :scope > select[id], :scope > textarea[id]') || label.querySelector('input[id], select[id], textarea[id]');
  return control?.id || '';
}

function addMetadataFieldInfoIcons() {
  const panel = $('metadataPanel');
  if (!panel || panel.dataset.infoIconsReady === 'true') return;
  const labels = [...panel.querySelectorAll('label')].filter(label => !label.closest('.meta-multi-panel'));
  labels.forEach(label => {
    if (label.querySelector(':scope > .metadata-info-button')) return;
    const fieldId = metadataHelpFieldIdForLabel(label);
    const helpText = metadataFieldHelpText(fieldId);
    if (!fieldId || !helpText) return;
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'metadata-info-button';
    button.dataset.metadataHelpFor = fieldId;
    button.setAttribute('aria-expanded', 'false');
    button.setAttribute('aria-label', `About ${label.firstChild?.textContent?.trim() || 'this field'}`);
    button.title = 'About this field';
    button.textContent = 'i';
    const help = document.createElement('span');
    help.className = 'metadata-info-popover hidden';
    help.dataset.metadataHelpTextFor = fieldId;
    help.textContent = helpText;
    const anchor = label.querySelector(':scope > input, :scope > select, :scope > textarea, :scope > .meta-multi-select') || null;
    label.insertBefore(button, anchor);
    label.insertBefore(help, anchor);
  });
  panel.dataset.infoIconsReady = 'true';
  refreshMetadataFieldInfoDescriptions();
}

function refreshMetadataFieldInfoDescriptions(kind = $('editKind')?.value || state.selected?.kind || '') {
  const panel = $('metadataPanel');
  if (!panel) return;
  panel.querySelectorAll('[data-metadata-help-text-for]').forEach(help => {
    const fieldId = help.dataset.metadataHelpTextFor || '';
    help.textContent = metadataFieldHelpText(fieldId, kind) || help.textContent || '';
  });
}

function closeMetadataFieldInfoPopovers(except = null) {
  document.querySelectorAll('.metadata-info-popover:not(.hidden)').forEach(popover => {
    if (popover === except) return;
    popover.classList.add('hidden');
    const fieldId = popover.dataset.metadataHelpTextFor || '';
    const button = document.querySelector(`.metadata-info-button[data-metadata-help-for="${CSS.escape(fieldId)}"]`);
    button?.setAttribute('aria-expanded', 'false');
  });
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

  const titleLabel = $('editTitleLabel');
  if (titleLabel?.firstChild) {
    titleLabel.firstChild.nodeValue = isStrategyGuide ? 'Strategy Guide Title' : (isMagazine ? 'Entry Title' : (isManual ? 'Manual Title' : 'Title'));
  }
  const categoryLabel = $('editCategoryLabel');
  if (categoryLabel?.firstChild) {
    categoryLabel.firstChild.nodeValue = 'Preferred Platform';
  }
  categoryLabel?.classList.toggle('hidden', isMagazine);
  const regionLabel = $('editRegionLabel');
  if (regionLabel?.firstChild) regionLabel.firstChild.nodeValue = isMagazine ? 'Country of publication' : 'Region';
  const writerLabel = $('editWriterLabel');
  if (writerLabel?.firstChild) writerLabel.firstChild.nodeValue = isMagazine ? 'Editor' : 'Writer / Author';
  const extrasLabel = $('editIncludedExtrasLabel');
  if (extrasLabel?.firstChild) extrasLabel.firstChild.nodeValue = isMagazine ? 'Insert Details, comma-separated' : 'Physical Extras, comma-separated';
  if ($('editPlatformMatchLabel')) $('editPlatformMatchLabel').classList.add('hidden');
  $('metadataPanel')?.classList.toggle('metadata-kind-strategy', isStrategyGuide);
  $('editGameTitleLabel')?.classList.toggle('strategy-game-context-order', isStrategyGuide);
  if ($('editSeriesLabel')) $('editSeriesLabel').classList.toggle('hidden', isStrategyGuide || isManual || isMagazine);
  if ($('editYearLabel')) $('editYearLabel').classList.toggle('hidden', isManual || isMagazine || isStrategyGuide);
  if ($('editPublicationDateGuideLabel')) $('editPublicationDateGuideLabel').classList.toggle('hidden', !isStrategyGuide);
  if ($('editManualTitleLabel')) $('editManualTitleLabel').classList.add('hidden');
  refreshMetadataFieldInfoDescriptions(kind);
  updateEditionControls();
  updateMetadataExportButtonLabel(kind);
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

function numericInput(id) {
  const el = $(id);
  const raw = String(el?.value || '').trim();
  if (!raw) return 0;
  const n = Number(raw);
  return Number.isFinite(n) && n > 0 ? Math.round(n) : 0;
}

function yearFromText(value = '') {
  const match = String(value || '').match(/(?:19|20)\d{2}/);
  return match ? match[0] : '';
}

function itemFileNameOnly(item = {}) {
  const candidates = [
    item.fileName, item.FileName, item.name, item.Name,
    item.originalFileName, item.OriginalFileName,
    item.sourceFileName, item.SourceFileName,
    item.relativePath, item.RelativePath,
    item.path, item.Path
  ].filter(Boolean);

  const value = String(candidates[0] || displayTitle(item) || item.title || 'guidevault-item').trim();
  return value.split(/[\\/]/).pop() || value;
}

function metadataExportSourceFileName(item = {}) {
  const fileName = itemFileNameOnly(item);
  if (fileName && fileName !== 'guidevault-item') return fileName;
  const pathValue = String(item.path || item.Path || item.relativePath || item.RelativePath || item.libraryPath || item.LibraryPath || '').trim();
  if (pathValue) return pathValue.split(/[\\/]/).pop() || pathValue;
  return '';
}

function itemContentHash(item = {}) {
  return String(
    item.contentHash || item.ContentHash ||
    item.sha256 || item.Sha256 || item.SHA256 ||
    item.fileHash || item.FileHash ||
    item.hash || item.Hash ||
    item.archiveHash || item.ArchiveHash ||
    ''
  ).trim();
}

function metadataExportKindWord(kind = $('editKind')?.value || state.selected?.kind || '') {
  if (kind === 'Manual') return 'Manual';
  if (kind === 'Magazine') return 'Magazine';
  return 'Guide';
}

function updateMetadataExportButtonLabel(kind = $('editKind')?.value || state.selected?.kind || '') {
  const btn = $('exportGuideMetadataBtn');
  if (!btn) return;
  btn.textContent = `Export ${metadataExportKindWord(kind)} Metadata`;
  btn.title = `Write this ${metadataExportKindWord(kind).toLowerCase()} metadata into the archive/package as Guidevault JSON`;
}

function metadataExportSlug(value = '') {
  return String(value || 'guidevault-metadata')
    .toLowerCase()
    .replace(/['"]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 90) || 'guidevault-metadata';
}

function metadataExportSafeFileName(value = '') {
  let name = String(value || 'Guidevault Metadata')
    .replace(/[\\/:*?"<>|]+/g, ' - ')
    .replace(/[\u0000-\u001F]+/g, ' ')
    .replace(/\s+/g, ' ')
    .replace(/\s*-\s*/g, ' - ')
    .replace(/(?:\s+-\s+){2,}/g, ' - ')
    .trim()
    .replace(/^[-_.| ]+|[-_.| ]+$/g, '')
    .replace(/[. ]+$/g, '')
    .slice(0, 160)
    .trim()
    .replace(/^[-_.| ]+|[-_.| ]+$/g, '');
  if (/^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$/i.test(name)) name = `${name} File`;
  return name || 'Guidevault Metadata';
}

function metadataExportTitleFromMetadata(metadata = {}, item = {}) {
  return metadata.strategyGuideTitle
    || metadata.manualTitle
    || metadata.magazineTitle
    || metadata.title
    || displayTitle(item)
    || item.title
    || 'Guidevault Metadata';
}

function fileRenameCurrentSchema() {
  const field = $('fileRenameSchema');
  const fromField = field ? String(field.value || '') : '';
  if (fromField.trim()) return fromField;
  try {
    const saved = String(localStorage.getItem(GUIDEVAULT_FILENAME_SCHEMA_KEY) || '');
    if (saved.trim()) return saved;
  } catch {}
  return GUIDEVAULT_DEFAULT_FILENAME_SCHEMA;
}

function saveFileRenameSchema(value = '') {
  const raw = String(value || '');
  const schema = raw.trim() ? raw : GUIDEVAULT_DEFAULT_FILENAME_SCHEMA;
  try { localStorage.setItem(GUIDEVAULT_FILENAME_SCHEMA_KEY, schema); } catch {}
  const field = $('fileRenameSchema');
  if (field && field.value !== schema) field.value = schema;
  return schema;
}

function hydrateFileRenameSchema() {
  renderFilenameSchemaTokenButtons();
  const field = $('fileRenameSchema');
  if (!field) return;
  if (document.activeElement === field) return;
  let schema = GUIDEVAULT_DEFAULT_FILENAME_SCHEMA;
  try { schema = String(localStorage.getItem(GUIDEVAULT_FILENAME_SCHEMA_KEY) || '').trim() || schema; } catch {}
  if (!String(field.value || '').trim() || field.value === GUIDEVAULT_DEFAULT_FILENAME_SCHEMA) field.value = schema;
}

function metadataFileExtension(item = {}) {
  const format = String(item.format || item.Format || '').trim().toLowerCase();
  if (['cbz', 'cbr', 'pdf'].includes(format)) return `.${format}`;
  const fileName = metadataExportSourceFileName(item);
  const match = fileName.match(/\.[^.\/]+$/);
  return match ? match[0] : '';
}

const GUIDEVAULT_FILENAME_SCHEMA_TOKEN_GROUPS = [
  {
    id: 'common',
    label: 'Common',
    kinds: ['Manual', 'Strategy Guide', 'Magazine'],
    tokens: [
      { label: 'Main Title', token: '{title}' },
      { label: 'Platform', token: '{platform}' },
      { label: 'Publisher', token: '{publisher}' },
      { label: 'Year', token: '{year}' },
      { label: 'Region', token: '{region}' },
      { label: 'Language', token: '{language}' },
      { label: 'Content Type', token: '{kind}' }
    ]
  },
  {
    id: 'game',
    label: 'Game / release',
    kinds: ['Manual', 'Strategy Guide'],
    tokens: [
      { label: 'Game Title', token: '{gameTitle}' },
      { label: 'Franchise', token: '{franchise}' },
      { label: 'Game Publisher', token: '{gamePublisher}' },
      { label: 'Developer', token: '{developer}' },
      { label: 'Genre', token: '{genre}' },
      { label: 'Game Release Year', token: '{gameReleaseYear}' }
    ]
  },
  {
    id: 'strategy',
    label: 'Strategy guide',
    kinds: ['Strategy Guide'],
    tokens: [
      { label: 'Guide Title', token: '{strategyGuideTitle}' },
      { label: 'Guide Type', token: '{guideType}' },
      { label: 'Edition', token: '{edition}' },
      { label: 'ISBN', token: '{isbn}' },
      { label: 'ISBN-10', token: '{isbn10}' },
      { label: 'ISBN-13', token: '{isbn13}' }
    ]
  },
  {
    id: 'manual',
    label: 'Manual',
    kinds: ['Manual'],
    tokens: [
      { label: 'Manual Title', token: '{manualTitle}' },
      { label: 'Manual Type', token: '{manualType}' },
      { label: 'Control Scheme', token: '{controlScheme}' }
    ]
  },
  {
    id: 'magazine',
    label: 'Magazine',
    kinds: ['Magazine'],
    tokens: [
      { label: 'Magazine Title', token: '{magazineTitle}' },
      { label: 'Issue', token: '{issue}' },
      { label: 'Volume', token: '{volume}' },
      { label: 'Number', token: '{number}' },
      { label: 'Month', token: '{month}' },
      { label: 'Cover Story', token: '{coverStory}' }
    ]
  }
];

function normalizeFilenameSchemaKind(kind = '') {
  const k = String(kind || '').trim().toLowerCase();
  if (k === 'manual' || k === 'manuals') return 'Manual';
  if (k === 'magazine' || k === 'magazines') return 'Magazine';
  if (k === 'strategy guide' || k === 'strategy guides' || k === 'guide' || k === 'guides') return 'Strategy Guide';
  return '';
}

function filenameSchemaCurrentKind() {
  return normalizeFilenameSchemaKind($('editKind')?.value || state.selected?.kind || state.selected?.Kind || '');
}

function renderFilenameSchemaTokenButtons() {
  const container = $('fileRenameTokenButtons');
  if (!container) return;
  const kind = filenameSchemaCurrentKind();
  const renderKey = kind || 'all';
  if (container.dataset.kind === renderKey && container.innerHTML.trim()) return;
  const groups = GUIDEVAULT_FILENAME_SCHEMA_TOKEN_GROUPS.filter(group => !kind || group.kinds.includes(kind));
  container.innerHTML = groups.map(group => `
    <div class="filename-token-group" data-token-group="${escapeHtml(group.id)}">
      <div class="filename-token-group-title">${escapeHtml(group.label)}</div>
      <div class="filename-token-group-buttons">
        ${group.tokens.map(({ label, token }) => `<button type="button" class="filename-token-button" data-token="${escapeHtml(token)}" title="Insert ${escapeHtml(token)}">${escapeHtml(label)}</button>`).join('')}
      </div>
    </div>
  `).join('');
  container.dataset.kind = renderKey;
}

function insertFilenameSchemaToken(token = '') {
  const field = $('fileRenameSchema');
  if (!field || !token) return;
  const value = String(field.value || '');
  const start = typeof field.selectionStart === 'number' ? field.selectionStart : value.length;
  const end = typeof field.selectionEnd === 'number' ? field.selectionEnd : value.length;
  const before = value.slice(0, start);
  const after = value.slice(end);
  const needsLeadingSpace = before.length > 0 && !/[\s([{_./]$/.test(before);
  const needsTrailingSpace = after.length > 0 && !/^[\s)\]}_.]/.test(after);
  const insertion = `${needsLeadingSpace ? ' ' : ''}${token}${needsTrailingSpace ? ' ' : ''}`;
  field.value = `${before}${insertion}${after}`;
  const nextPos = before.length + insertion.length;
  field.focus();
  try { field.setSelectionRange(nextPos, nextPos); } catch {}
  updateMetadataFileMaintenance();
}

function normalizeFilenameSchemaTokenKey(key = '') {
  return String(key || '').trim().toLowerCase().replace(/[^a-z0-9]+/g, '');
}

function filenameSchemaTokenMap(metadata = {}, item = {}) {
  const title = metadataExportTitleFromMetadata(metadata, item);
  const platform = metadata.preferredPlatform || metadata.category || metadata.system || item.category || item.Category || '';
  const franchise = metadata.gameFranchise || metadata.franchise || metadata.series || item.series || item.Series || '';
  const guideType = Array.isArray(metadata.guideType) ? metadata.guideType.join(', ') : (metadata.guideType || '');
  const edition = metadata.edition || metadata.editionType || '';
  const isbn10 = metadata.isbn10 || item.isbn10 || item.Isbn10 || '';
  const isbn13 = metadata.isbn13 || item.isbn13 || item.Isbn13 || '';
  const isbn = metadata.isbn || isbn13 || isbn10 || '';
  const developer = metadata.developer || metadata.gameDeveloper || item.developer || item.Developer || item.gameDeveloper || item.GameDeveloper || '';
  const gamePublisher = metadata.gamePublisher || item.gamePublisher || item.GamePublisher || metadata.publisher || item.publisher || item.Publisher || '';

  return {
    title,
    mainTitle: title,
    guideTitle: title,
    strategyGuideTitle: metadata.strategyGuideTitle || title,
    manualTitle: metadata.manualTitle || title,
    magazineTitle: metadata.magazineTitle || title,
    gameTitle: metadata.gameTitle || metadata.coverSubject || title,
    platform,
    preferredPlatform: platform,
    publisher: metadata.publisher || item.publisher || item.Publisher || '',
    year: metadata.year || item.year || item.Year || '',
    issue: metadata.issueNumber || item.issueNumber || item.IssueNumber || '',
    issueNumber: metadata.issueNumber || item.issueNumber || item.IssueNumber || '',
    volume: metadata.volume || item.volume || item.Volume || '',
    number: metadata.number || item.number || item.Number || '',
    month: metadata.month || metadata.publicationMonth || item.month || item.Month || item.publicationMonth || item.PublicationMonth || '',
    kind: metadata.kind || item.kind || item.Kind || '',
    contentType: metadata.kind || item.kind || item.Kind || '',
    region: metadata.region || item.region || item.Region || '',
    language: metadata.language || metadata.languageTag || item.languageTag || item.LanguageTag || '',
    edition: Array.isArray(edition) ? edition.join(', ') : edition,
    editionType: Array.isArray(metadata.editionType) ? metadata.editionType.join(', ') : (metadata.editionType || ''),
    editionYear: metadata.editionYear || '',
    editionVolume: metadata.editionVolume || '',
    guideType,
    franchise,
    gameFranchise: franchise,
    gamePublisher,
    developer,
    gameDeveloper: developer,
    genre: metadata.genre || item.genre || item.Genre || '',
    gameReleaseYear: metadata.gameReleaseYear || item.gameReleaseYear || item.GameReleaseYear || '',
    isbn,
    isbn10,
    isbn13,
    asin: metadata.asin || item.asin || item.Asin || '',
    manualType: metadata.manualType || item.manualType || item.ManualType || '',
    controlScheme: metadata.controlScheme || item.controlScheme || item.ControlScheme || '',
    coverStory: metadata.coverStory || item.coverStory || item.CoverStory || ''
  };
}

function applyFilenameSchema(schema = GUIDEVAULT_DEFAULT_FILENAME_SCHEMA, metadata = {}, item = {}) {
  const tokens = filenameSchemaTokenMap(metadata, item);
  const normalizedTokens = Object.entries(tokens).reduce((acc, [key, value]) => {
    acc[normalizeFilenameSchemaTokenKey(key)] = value;
    return acc;
  }, {});
  const template = String(schema || GUIDEVAULT_DEFAULT_FILENAME_SCHEMA).trim() ? String(schema || GUIDEVAULT_DEFAULT_FILENAME_SCHEMA) : GUIDEVAULT_DEFAULT_FILENAME_SCHEMA;
  const expanded = template.replace(/\{([^{}]+)\}/g, (_, key) => {
    const rawKey = String(key || '').trim();
    const camelKey = rawKey ? rawKey.charAt(0).toLowerCase() + rawKey.slice(1) : rawKey;
    const value = tokens[rawKey] ?? tokens[camelKey] ?? normalizedTokens[normalizeFilenameSchemaTokenKey(rawKey)] ?? '';
    return String(value || '').trim();
  });
  const cleaned = expanded
    .replace(/\s+/g, ' ')
    .replace(/\s+-\s+(?=$)/g, ' ')
    .replace(/^\s*-\s+/g, '')
    .replace(/\s+\|\s+(?=$)/g, ' ')
    .replace(/^\s*\|\s+/g, '')
    .replace(/\s+-\s+/g, ' - ')
    .replace(/\s+\|\s+/g, ' | ')
    .trim()
    .replace(/^[-_.| ]+|[-_.| ]+$/g, '');
  return cleaned || metadataExportTitleFromMetadata(metadata, item);
}

function metadataExportSuggestedFileName(metadata = {}, item = {}, schema = null) {
  const extension = metadataFileExtension(item).replace(/^\./, '');
  const nameSource = schema ? applyFilenameSchema(schema, metadata, item) : metadataExportTitleFromMetadata(metadata, item);
  const title = metadataExportSafeFileName(nameSource);
  return extension ? `${title}.${extension}` : title;
}

function downloadJsonFile(filename, data) {
  const json = JSON.stringify(data, null, 2);
  const blob = new Blob([json], { type: 'application/json;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  window.setTimeout(() => {
    URL.revokeObjectURL(url);
    link.remove();
  }, 0);
}

function buildCurrentMetadataPayloadFromForm(extra = {}) {
  const selectedKind = $('editKind')?.value || state.selected?.kind || 'Strategy Guide';
  const tags = $('editTags') ? $('editTags').value.split(',').map(t => t.trim()).filter(Boolean) : [];
  const associatedPlatforms = $('editAssociatedPlatforms') ? $('editAssociatedPlatforms').value.split(',').map(p => p.trim()).filter(Boolean) : [];
  const preferredPlatform = selectedKind === 'Magazine'
    ? ''
    : (selectedKind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(associatedPlatforms)
      ? MULTI_PLATFORM_LABEL
      : ($('editCategory')?.value || ''));

  const magazineTitleValue = selectedKind === 'Magazine' ? ($('editMagazineTitle')?.value || $('editSeries')?.value || $('editTitle')?.value || '') : '';
  const magazinePayload = selectedKind === 'Magazine' ? {
    magazineTitle: magazineTitleValue,
    series: magazineTitleValue,
    volume: $('editVolume')?.value || '',
    coverDate: $('editCoverDate')?.value || '',
    barcodeUpcIssn: $('editBarcodeUpcIssn')?.value || '',
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
    strategyGuideTitle: $('editTitle')?.value || '',
    gameTitle: $('editGameTitle')?.value || $('editPlatformMatchTitle')?.value || '',
    isbn: rawIsbn,
    isbn10: isbnParts.isbn10,
    isbn13: isbnParts.isbn13,
    guideType: getMultiSelectValues('editGuideType'),
    editionType: getMultiSelectValues('editEditionType'),
    editionYear: $('editEditionYear')?.value || '',
    editionVolume: $('editEditionVolume')?.value || '',
    edition: buildEditionValue(getMultiSelectValues('editEditionType'), $('editEditionYear')?.value || '', $('editEditionVolume')?.value || ''),
    publicationDate: $('editPublicationDateGuide')?.value || '',
    region: $('editRegion')?.value || '',
    gameFranchise: $('editFranchise')?.value || $('editSeries')?.value || '',
    gameDeveloper: $('editDeveloper')?.value || '',
    gamePublisher: $('editGamePublisher')?.value || '',
    gameReleaseYear: $('editGameReleaseYear')?.value || '',
    genre: $('editGenre')?.value || '',
    coveredGames: csvInput('editCoveredGames'),
    coveredPlatforms: csvInput('editCoveredPlatforms'),
    guideTopics: csvInput('editGuideTopics'),
    specialFeatures: csvInput('editStrategySpecialFeatures'),
    charactersCovered: csvInput('editCharactersCovered'),
    locationsCovered: csvInput('editLocationsCovered')
  } : {};

  const manualPayload = selectedKind === 'Manual' ? {
    manualTitle: $('editTitle')?.value || '',
    manualType: $('editManualType')?.value || 'Instruction Manual',
    gameTitle: $('editGameTitle')?.value || $('editSeries')?.value || $('editTitle')?.value || '',
    publicationDate: '',
    region: $('editRegion')?.value || '',
    gameFranchise: $('editFranchise')?.value || $('editSeries')?.value || '',
    gameDeveloper: $('editDeveloper')?.value || '',
    gamePublisher: $('editGamePublisher')?.value || '',
    gameReleaseYear: $('editGameReleaseYear')?.value || '',
    genre: $('editGenre')?.value || '',
    includedSections: csvInput('editIncludedSections'),
    includedExtras: csvInput('editIncludedExtras'),
    controlScheme: $('editControlScheme')?.value || '',
    charactersCovered: csvInput('editCharactersCovered'),
    itemsCovered: csvInput('editItemsCovered'),
    warrantySupport: $('editWarrantySupport')?.value || ''
  } : {};

  return normalizeClientMetadataPayload({
    title: $('editTitle')?.value || '',
    kind: selectedKind,
    category: preferredPlatform,
    preferredPlatform,
    system: preferredPlatform,
    associatedPlatforms: selectedKind === 'Strategy Guide' ? associatedPlatforms : [],
    series: $('editSeries')?.value || '',
    issueNumber: selectedKind === 'Magazine' ? ($('editIssue')?.value || '') : '',
    publisher: $('editPublisher')?.value || '',
    year: selectedKind === 'Manual' || selectedKind === 'Strategy Guide' ? '' : (selectedKind === 'Magazine' ? (yearFromText($('editCoverDate')?.value || $('editPublicationDate')?.value || $('editYear')?.value || '') || $('editYear')?.value || '') : ($('editYear')?.value || '')),
    pageCount: numericInput('editPageCount'),
    metadataPageCount: numericInput('editPageCount'),
    writer: $('editWriter')?.value || '',
    rating: selectedKind === 'Magazine' ? '' : ($('editEsrbRating')?.value || ''),
    language: $('editLanguageTag')?.value || '',
    languageTag: $('editLanguageTag')?.value || '',
    summary: $('editSummary')?.value || '',
    tags,
    notes: $('notesText')?.value || '',
    metadataLocks: currentMetadataLocksPayload(),
    metadataStatus: normalizeMetadataStatus($('editMetadataStatus')?.value || state.selected?.metadataStatus || 'Unreviewed'),
    ...magazinePayload,
    ...strategyPayload,
    ...manualPayload,
    ...extra
  });
}

function buildGuidevaultItemMetadataExport() {
  if (!state.selected) return null;
  const item = applyClientMetadataOverride(state.selected);
  const metadata = buildCurrentMetadataPayloadFromForm();
  const suggestedFileName = metadataExportSuggestedFileName(metadata, item);
  const contentHash = itemContentHash(item);
  const fileSizeBytes = Number(item.sizeBytes ?? item.SizeBytes ?? item.fileSizeBytes ?? item.FileSizeBytes ?? 0) || 0;
  const pageCount = Number(metadata.pageCount || metadata.metadataPageCount || 0) || 0;
  const format = item.format || item.Format || '';

  const exportedItem = {
    suggestedFileName,
    fileSizeBytes,
    format,
    kind: metadata.kind || item.kind || '',
    metadata
  };

  if (contentHash) exportedItem.contentHash = contentHash;
  if (pageCount > 0) exportedItem.pageCount = pageCount;

  return {
    schema: 'guidevault.item-metadata.v1',
    exportedAt: new Date().toISOString(),
    guidevaultVersion: GUIDEVAULT_APP_VERSION,
    exportScope: 'item',
    item: exportedItem
  };
}

function setMetadataExportStatus(message = '', tone = '') {
  const status = $('metadataExportStatus');
  if (!status) return;
  status.textContent = String(message || '');
  status.classList.toggle('error', tone === 'error');
  status.classList.toggle('success', tone === 'success');
}

async function exportSelectedGuidevaultMetadata() {
  if (!state.selected) return;
  const btn = $('exportGuideMetadataBtn');
  const originalText = btn?.textContent || '';
  const selectedId = String(state.selected.id || state.selected.Id || '').trim();
  if (!selectedId) return;

  try {
    if (btn) {
      btn.disabled = true;
      btn.textContent = 'Writing...';
    }
    setMetadataExportStatus('Writing Guidevault metadata into the source package...', '');

    const metadata = buildCurrentMetadataPayloadFromForm({ metadataSource: 'Guidevault JSON' });
    const optimistic = mergeSavedMetadataClientSide(state.selected, {}, metadata);
    optimistic.id = optimistic.id || selectedId;
    optimistic.Id = optimistic.Id || selectedId;
    replaceItemInState(optimistic);
    state.selected = optimistic;
    rememberClientMetadataOverride(selectedId, metadata);

    const res = await fetch(`/api/items/${encodeURIComponent(selectedId)}/metadata/native-export`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(metadata)
    });

    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || data?.message || `Metadata export failed. HTTP ${res.status}`);

    if (data?.item) {
      replaceItemInState(data.item);
      state.selected = data.item;
      renderDetails(data.item);
      activateTab(state.activeTab || 'metadata');
    }

    const message = data?.message || `Wrote ${data?.metadataFileName || 'Guidevault metadata'} into the item package.`;
    setStatus(message);
    setMetadataExportStatus(message, 'success');
    if (btn) {
      btn.textContent = 'Exported';
      window.setTimeout(() => { if (btn) btn.textContent = originalText || `Export ${metadataExportKindWord(metadata.kind)} Metadata`; }, 1200);
    }
  } catch (err) {
    console.error('Guidevault metadata export failed', err);
    const message = `Unable to export Guidevault metadata: ${err?.message || err}`;
    setStatus(message);
    setMetadataExportStatus(message, 'error');
    if (typeof showAppConfirm === 'function') {
      await showAppConfirm({
        title: 'Metadata export failed',
        message,
        okText: 'OK',
        cancelText: 'Close'
      });
    }
    if (btn) btn.textContent = 'Export Failed';
  } finally {
    if (btn) {
      window.setTimeout(() => {
        btn.disabled = false;
        if (btn.textContent === 'Export Failed') btn.textContent = originalText || 'Export Metadata';
      }, 900);
    }
  }
}

async function enrichSelectedFileMetadata() {
  if (!state.selected) return;
  const selectedId = String(state.selected.id || state.selected.Id || '').trim();
  if (!selectedId) return;

  const btn = $('enrichCurrentFileMetadataBtn');
  const originalText = btn?.textContent || 'Import Guidevault metadata JSON';
  const statusEl = $('metadataExportStatus') || $('fileRenameStatus');

  try {
    if (btn) {
      btn.disabled = true;
      btn.textContent = 'Importing...';
    }
    if (statusEl) {
      statusEl.textContent = 'Importing Guidevault metadata JSON from this package...';
      statusEl.classList.remove('error');
      statusEl.classList.remove('success');
    }

    const res = await fetch(`/api/items/${encodeURIComponent(selectedId)}/metadata/enrich-native`, {
      method: 'POST',
      cache: 'no-store'
    });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || data?.message || `Single-file enrichment failed. HTTP ${res.status}`);

    if (data?.item) {
      replaceItemInState(data.item);
      state.selected = data.item;
      clearClientMetadataOverride(selectedId);
      renderDetails(data.item);
      activateTab('library-data');
      applyFilters();
    }

    const message = data?.message || 'Guidevault metadata JSON import complete.';
    setStatus(message);
    if (statusEl) {
      statusEl.textContent = message;
      statusEl.classList.add('success');
      statusEl.classList.remove('error');
    }
    if (btn) {
      btn.textContent = 'Imported';
      window.setTimeout(() => { if (btn) btn.textContent = originalText; }, 1200);
    }
  } catch (err) {
    console.error('Single-file metadata enrichment failed', err);
    const message = `Unable to import Guidevault metadata JSON: ${err?.message || err}`;
    setStatus(message);
    if (statusEl) {
      statusEl.textContent = message;
      statusEl.classList.add('error');
      statusEl.classList.remove('success');
    }
    if (btn) btn.textContent = 'Import Failed';
  } finally {
    if (btn) {
      window.setTimeout(() => {
        btn.disabled = false;
        if (btn.textContent === 'Import Failed') btn.textContent = originalText;
      }, 900);
    }
  }
}


const OPEN_LIBRARY_IMPORT_FIELDS = [
  { key: 'title', label: 'Title', payloadKey: 'title' },
  { key: 'authorWriter', label: 'Author / Writer', payloadKey: 'writer' },
  { key: 'publisher', label: 'Publisher', payloadKey: 'publisher' },
  { key: 'publishYear', label: 'Publish Year', payloadKey: 'year' },
  { key: 'isbn10', label: 'ISBN-10', payloadKey: 'isbn10' },
  { key: 'isbn13', label: 'ISBN-13', payloadKey: 'isbn13' },
  { key: 'language', label: 'Language', payloadKey: 'languageTag' },
  { key: 'summary', label: 'Description / Summary', payloadKey: 'summary' },
  { key: 'pageCount', label: 'Page Count', payloadKey: 'pageCount' }
];

function normalizeOpenLibraryLanguage(value = '') {
  const raw = String(value || '').trim();
  if (!raw) return '';
  const key = raw.toLowerCase();
  if (['en','eng','english'].includes(key)) return 'English';
  if (['ja','jpn','japanese'].includes(key)) return 'Japanese';
  if (['fr','fre','fra','french'].includes(key)) return 'French';
  if (['de','ger','deu','german'].includes(key)) return 'German';
  if (['es','spa','spanish'].includes(key)) return 'Spanish';
  if (['it','ita','italian'].includes(key)) return 'Italian';
  return raw;
}


const STRATEGY_GUIDE_METADATA_SOURCE_LOOKUPS = new Set(['openLibrary', 'igdb', 'esrb']);

function metadataLookupSelectedItemKey(item = state.selected || {}) {
  try {
    return String(itemIdOf(item) || item?.id || item?.Id || item?.relativePath || item?.filePath || item?.path || item?.title || '').trim();
  } catch {
    return String(item?.id || item?.Id || item?.relativePath || item?.filePath || item?.path || item?.title || '').trim();
  }
}

function resetIndividualMetadataLookupState(sourceKey, item = state.selected || {}, clearResults = true) {
  const bucket = state[sourceKey];
  if (!bucket) return;
  bucket.itemKey = metadataLookupSelectedItemKey(item);
  bucket.step = 'search';
  bucket.selectedResult = null;
  bucket.resolvedResult = null;
  bucket.forceInitialValues = true;
  if (clearResults) bucket.results = [];
}

function resetIndividualMetadataLookupsForItem(item = state.selected || {}) {
  const itemKey = metadataLookupSelectedItemKey(item);
  ['openLibrary', 'igdb', 'esrb'].forEach(sourceKey => {
    const bucket = state[sourceKey];
    if (!bucket || bucket.itemKey === itemKey) return;
    resetIndividualMetadataLookupState(sourceKey, item, true);
  });
}

function metadataSourceLookupAllowedForSelectedItem(sourceKey = '') {
  const item = state.selected || {};
  if (!STRATEGY_GUIDE_METADATA_SOURCE_LOOKUPS.has(sourceKey)) return false;
  return item.kind === 'Strategy Guide';
}

function metadataSourceLookupUnavailableMessage(sourceLabel = 'This metadata lookup') {
  return `${sourceLabel} is currently available for Strategy Guides only. Item-specific lookup flows for Manuals and Magazines can be added later with their own search criteria.`;
}

function updateMetadataSourceActionVisibility() {
  const actions = [
    { id: 'openLibrarySearchBtn', source: 'openLibrary', label: 'Open Library metadata lookup' },
    { id: 'igdbSearchBtn', source: 'igdb', label: 'IGDB game metadata lookup' },
    { id: 'esrbSearchBtn', source: 'esrb', label: 'ESRB rating lookup' }
  ];
  actions.forEach(action => {
    const button = $(action.id);
    if (!button) return;
    const allowed = metadataSourceLookupAllowedForSelectedItem(action.source);
    button.classList.toggle('hidden', !allowed);
    button.disabled = !allowed;
    button.setAttribute('aria-hidden', allowed ? 'false' : 'true');
    button.title = allowed
      ? (button.dataset.defaultTitle || button.title || action.label)
      : metadataSourceLookupUnavailableMessage(action.label);
  });
}

function isOpenLibraryBlank(value, key = '') {
  if (value === null || value === undefined) return true;
  if (key === 'pageCount') return !(Number(value) > 0);
  const text = String(value || '').trim();
  return !text || text === '\u2014' || text.toLowerCase() === 'unknown';
}

function openLibraryCleanValue(key, value) {
  if (key === 'language') return normalizeOpenLibraryLanguage(value);
  if (key === 'pageCount') {
    const n = Number(value || 0);
    return Number.isFinite(n) && n > 0 ? Math.round(n) : '';
  }
  return String(value ?? '').trim();
}

function openLibrarySameText(a = '', b = '') {
  const clean = value => String(value || '').toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim();
  return clean(a) && clean(a) === clean(b);
}

function openLibraryCurrentMetadataForCompare() {
  let form = {};
  try { form = buildCurrentMetadataPayloadFromForm(); } catch { form = {}; }
  const item = state.selected || {};
  const rawIsbn = form.isbn || combinedIsbnText(item) || `${form.isbn10 || ''} / ${form.isbn13 || ''}`;
  const isbnParts = splitIsbnInput(rawIsbn);
  return {
    title: form.title || item.title || '',
    authorWriter: form.writer || item.writer || '',
    publisher: form.publisher || item.publisher || '',
    publishYear: form.year || item.year || '',
    isbn10: form.isbn10 || isbnParts.isbn10 || item.isbn10 || '',
    isbn13: form.isbn13 || isbnParts.isbn13 || item.isbn13 || '',
    language: form.languageTag || item.languageTag || '',
    summary: form.summary || item.summary || '',
    pageCount: form.pageCount || item.pageCount || ''
  };
}

function openLibraryInitialSearchValues() {
  let form = {};
  try { form = buildCurrentMetadataPayloadFromForm(); } catch { form = {}; }
  const item = applyClientMetadataOverride(state.selected || {});
  const title = String(
    item.strategyGuideTitle || item.manualTitle || item.magazineTitle || item.title || displayTitle(item) ||
    form.strategyGuideTitle || form.manualTitle || form.magazineTitle || form.title || ''
  ).trim();
  const gameTitle = String(item.gameTitle || item.platformMatchTitle || item.series || form.gameTitle || '').trim();
  const publisher = String(item.publisher || form.publisher || '').trim();
  const year = String(item.year || item.editionYear || form.year || form.editionYear || '').trim();
  const rawIsbn = String(combinedIsbnText(item) || form.isbn || `${form.isbn10 || ''} ${form.isbn13 || ''}` || '').trim();
  const isbnParts = splitIsbnInput(rawIsbn);
  const isbn = isbnParts.isbn13 || isbnParts.isbn10 || '';
  const secondary = isbn || year || '';
  return {
    q: title || gameTitle || [publisher, year].filter(Boolean).join(' '),
    secondary,
    isbn,
    title,
    gameTitle,
    publisher,
    year
  };
}

function openLibraryResultTitle(result = {}) {
  return [result.title, result.authorWriter, result.publishYear].filter(Boolean).join(' - ') || 'Open Library result';
}

function ensureOpenLibraryMetadataUi() {
  if (!$('openLibraryDialog')) {
    const dialog = document.createElement('dialog');
    dialog.id = 'openLibraryDialog';
    dialog.className = 'openlibrary-dialog';
    dialog.innerHTML = `
      <div class="openlibrary-modal">
        <header class="openlibrary-modal-head">
          <div>
            <h3>Search Open Library Metadata</h3>
            <p>Search by strategy guide title first. Use ISBN or year as a secondary hint. Review existing Guidevault values beside Open Library values before importing.</p>
          </div>
          <button type="button" id="openLibraryCloseBtn" class="ghost tiny">Close</button>
        </header>
        <div id="openLibraryDialogBody" class="openlibrary-modal-body"></div>
      </div>`;
    document.body.appendChild(dialog);
  }

  const existing = $('openLibrarySearchBtn');
  if (existing) {
    existing.classList.remove('ghost');
    existing.classList.add('metadata-lookup-button', 'openlibrary-action-button');
    existing.type = 'button';
    existing.textContent = 'Open Library';
    existing.dataset.defaultTitle = 'Search Open Library by strategy guide title/game title and review metadata before importing selected fields.';
    updateMetadataSourceActionVisibility();
    return;
  }

  const target = $('metadataPanel');
  if (target) {
    const btn = document.createElement('button');
    btn.id = 'openLibrarySearchBtn';
    btn.type = 'button';
    btn.className = 'metadata-lookup-button openlibrary-action-button';
    btn.textContent = 'Open Library';
    btn.title = 'Search Open Library by strategy guide title/game title and review metadata before importing selected fields.';
    btn.dataset.defaultTitle = btn.title;
    target.appendChild(btn);
    updateMetadataSourceActionVisibility();
  }
}

function openLibrarySetStatus(message = '', tone = '') {
  const el = $('openLibraryStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}

function openLibrarySearchPanelHtml() {
  const initial = openLibraryInitialSearchValues();
  const forceInitial = state.openLibrary?.forceInitialValues || state.openLibrary?.itemKey !== metadataLookupSelectedItemKey();
  const primaryValue = forceInitial ? (initial.q || '') : ($('openLibrarySearchInput')?.value || initial.q || '');
  const secondaryValue = forceInitial ? (initial.secondary || '') : ($('openLibrarySecondaryInput')?.value || initial.secondary || '');
  return `<section class="openlibrary-search-panel">
    <div class="openlibrary-search-fields">
      <label>Title / primary search
        <input id="openLibrarySearchInput" type="search" value="${escapeForAttribute(primaryValue)}" placeholder="Strategy guide title or game title" />
      </label>
      <label>Secondary hint
        <input id="openLibrarySecondaryInput" type="search" value="${escapeForAttribute(secondaryValue)}" placeholder="Optional: ISBN or year" />
      </label>
      <button type="button" id="openLibraryRunSearchBtn" class="primary">Search</button>
    </div>
    <p class="openlibrary-help">Guidevault searches the title field first, then uses only ISBN or year as the secondary hint. Game title and publisher are not prefilled here.</p>
    <p id="openLibraryStatus" class="openlibrary-status"></p>
    <div id="openLibraryResults" class="openlibrary-results"></div>
  </section>`;
}

function renderOpenLibrarySearchResults(results = state.openLibrary.results || []) {
  const host = $('openLibraryResults');
  if (!host) return;
  if (!results.length) {
    host.innerHTML = '<div class="openlibrary-empty">No Open Library results yet. Search by strategy guide title first; use only ISBN or year as the secondary hint when available.</div>';
    return;
  }
  host.innerHTML = results.map((result, index) => `
    <article class="openlibrary-result-card">
      <div class="openlibrary-result-cover">${result.coverPreviewUrl ? `<img src="${escapeForAttribute(result.coverPreviewUrl)}" alt="" loading="lazy" />` : '<span>No cover</span>'}</div>
      <div class="openlibrary-result-main">
        <h4>${escapeHtml(result.title || 'Untitled')}</h4>
        <p>${escapeHtml([result.authorWriter, result.publisher, result.publishYear].filter(Boolean).join(' - ') || 'No extra result details')}</p>
        <small>${escapeHtml(result.matchBy || 'Search')} match - ${escapeHtml(result.confidence || 'Unknown')} confidence${result.isbn10 ? ` - ISBN-10 ${escapeHtml(result.isbn10)}` : ''}${result.isbn13 ? ` - ISBN-13 ${escapeHtml(result.isbn13)}` : ''}</small>
      </div>
      <button type="button" class="ghost openlibrary-select-result" data-result-index="${index}">Select</button>
    </article>`).join('');
}

function renderOpenLibraryDialog(step = state.openLibrary.step || 'search') {
  ensureOpenLibraryMetadataUi();
  const body = $('openLibraryDialogBody');
  if (!body) return;
  state.openLibrary.step = step;
  if (step === 'compare' && state.openLibrary.resolvedResult) {
    body.innerHTML = openLibraryComparisonHtml(state.openLibrary.resolvedResult);
    return;
  }
  body.innerHTML = openLibrarySearchPanelHtml();
  renderOpenLibrarySearchResults(state.openLibrary.results || []);
}

async function openOpenLibraryMetadataDialog() {
  if (!state.selected) return;
  ensureOpenLibraryMetadataUi();
  resetIndividualMetadataLookupState('openLibrary', state.selected, true);
  renderOpenLibraryDialog('search');
  state.openLibrary.forceInitialValues = false;
  const dialog = $('openLibraryDialog');
  try {
    if (dialog && !dialog.open) dialog.showModal();
  } catch {
    if (dialog) dialog.setAttribute('open', 'open');
  }
  await runOpenLibraryMetadataSearch(true);
}

function closeOpenLibraryDialog() {
  const dialog = $('openLibraryDialog');
  if (!dialog) return;
  try { dialog.close(); } catch { dialog.removeAttribute('open'); }
}

async function runOpenLibraryMetadataSearch(useInitial = false) {
  ensureOpenLibraryMetadataUi();
  const initial = openLibraryInitialSearchValues();
  const input = $('openLibrarySearchInput');
  const secondaryInput = $('openLibrarySecondaryInput');
  const primary = String((useInitial ? initial.q : input?.value) || initial.q || '').trim();
  const secondary = String((useInitial ? initial.secondary : secondaryInput?.value) || initial.secondary || '').trim();
  if (!primary && !secondary) {
    openLibrarySetStatus('Enter a strategy guide title, year, or ISBN to search Open Library.', 'error');
    return;
  }
  if (input && !input.value) input.value = primary;
  if (secondaryInput && !secondaryInput.value) secondaryInput.value = secondary;
  openLibrarySetStatus('Searching Open Library...', 'info');
  const params = new URLSearchParams({ q: primary || secondary, limit: '16' });
  if (secondary) params.set('secondary', secondary);
  const typedIsbn = splitIsbnInput(`${primary || ''} ${secondary || ''}`);
  if (typedIsbn.isbn10 || typedIsbn.isbn13) params.set('isbn', typedIsbn.isbn13 || typedIsbn.isbn10);
  if (initial.title) params.set('title', initial.title);
  if (initial.gameTitle) params.set('gameTitle', initial.gameTitle);
  if (initial.publisher) params.set('publisher', initial.publisher);
  if (initial.year) params.set('year', initial.year);
  try {
    const res = await fetch(`/api/openlibrary/search?${params.toString()}`, { cache: 'no-store' });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || `Open Library search failed. HTTP ${res.status}`);
    state.openLibrary.results = Array.isArray(data?.results) ? data.results : [];
    renderOpenLibrarySearchResults(state.openLibrary.results);
    openLibrarySetStatus(state.openLibrary.results.length ? `Found ${state.openLibrary.results.length} result(s). Select the closest match to compare fields.` : 'No Open Library results found. Try the game title by itself, or remove publisher/year hints.', state.openLibrary.results.length ? 'success' : 'error');
  } catch (err) {
    console.error('Open Library search failed', err);
    openLibrarySetStatus(`Open Library search failed: ${err?.message || err}`, 'error');
  }
}

async function selectOpenLibrarySearchResult(index) {
  const result = state.openLibrary.results?.[Number(index)];
  if (!result) return;
  state.openLibrary.selectedResult = result;
  openLibrarySetStatus(`Loading details for ${openLibraryResultTitle(result)}...`, 'info');
  try {
    const res = await fetch('/api/openlibrary/resolve', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(result)
    });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || `Open Library detail lookup failed. HTTP ${res.status}`);
    state.openLibrary.resolvedResult = data;
    renderOpenLibraryDialog('compare');
  } catch (err) {
    console.error('Open Library detail lookup failed', err);
    openLibrarySetStatus(`Open Library detail lookup failed: ${err?.message || err}`, 'error');
  }
}

function openLibraryComparisonHtml(result = {}) {
  const current = openLibraryCurrentMetadataForCompare();
  const proposed = {
    title: openLibraryCleanValue('title', result.title),
    authorWriter: openLibraryCleanValue('authorWriter', result.authorWriter),
    publisher: openLibraryCleanValue('publisher', result.publisher),
    publishYear: openLibraryCleanValue('publishYear', result.publishYear),
    isbn10: openLibraryCleanValue('isbn10', result.isbn10),
    isbn13: openLibraryCleanValue('isbn13', result.isbn13),
    language: openLibraryCleanValue('language', result.language),
    summary: openLibraryCleanValue('summary', result.summary),
    pageCount: openLibraryCleanValue('pageCount', result.pageCount)
  };
  const currentCover = state.selected ? coverUrl(state.selected) : '';
  const rows = OPEN_LIBRARY_IMPORT_FIELDS.map(field => openLibraryComparisonRowHtml(field, current, proposed)).join('');
  return `<section class="openlibrary-compare-panel">
    <button type="button" id="openLibraryBackToResultsBtn" class="ghost tiny">Back to results</button>
    <div class="openlibrary-cover-compare">
      <figure><figcaption>Guidevault Cover</figcaption>${currentCover ? `<img src="${escapeForAttribute(currentCover)}" alt="Current Guidevault cover" />` : '<span>No Guidevault cover</span>'}</figure>
      <figure><figcaption>Open Library Cover <em>Preview only - not imported</em></figcaption>${result.coverPreviewUrl ? `<img src="${escapeForAttribute(result.coverPreviewUrl)}" alt="Open Library cover preview" />` : '<span>No Open Library cover</span>'}</figure>
    </div>
    <div class="openlibrary-selected-source">
      <h4>${escapeHtml(result.title || 'Selected Open Library result')}</h4>
      <p>${escapeHtml([result.authorWriter, result.publisher, result.publishYear].filter(Boolean).join(' - ') || 'Review fields before importing.')}</p>
    </div>
    <div class="openlibrary-table-wrap">
      <table class="openlibrary-comparison-table">
        <thead><tr><th>Import</th><th>Field</th><th>Existing Guidevault Value</th><th>Open Library Value</th></tr></thead>
        <tbody>${rows}</tbody>
      </table>
    </div>
    <p id="openLibraryStatus" class="openlibrary-status"></p>
    <footer class="openlibrary-import-actions">
      <button type="button" id="openLibraryImportSelectedBtn" class="primary">Import Selected Fields</button>
      <button type="button" id="openLibraryImportEmptyBtn" class="ghost">Import Empty Fields Only</button>
      <button type="button" id="openLibraryImportAllBtn" class="ghost">Import All Fields</button>
      <button type="button" id="openLibraryCancelBtn" class="ghost">Cancel</button>
    </footer>
  </section>`;
}

function openLibraryComparisonRowHtml(field, current, proposed) {
  const existing = openLibraryCleanValue(field.key, current[field.key]);
  const incoming = openLibraryCleanValue(field.key, proposed[field.key]);
  const hasIncoming = !isOpenLibraryBlank(incoming, field.key);
  const existingBlank = isOpenLibraryBlank(existing, field.key);
  const differs = hasIncoming && String(existing || '').trim() !== String(incoming || '').trim();
  const locked = isMetadataFieldLocked(state.selected || {}, field.payloadKey || field.key);
  const checked = hasIncoming && existingBlank && !locked ? 'checked' : '';
  const disabled = hasIncoming && !locked ? '' : 'disabled';
  return `<tr class="${differs ? 'different' : ''} ${!hasIncoming ? 'missing-incoming' : ''} ${locked ? 'locked-field' : ''}">
    <td><input type="checkbox" data-openlibrary-field="${escapeForAttribute(field.key)}" ${checked} ${disabled} /></td>
    <td>${locked ? '<span class="metadata-lock-inline" title="Locked field - skipped by imports">\uD83D\uDD12</span> ' : ''}${escapeHtml(field.label)}</td>
    <td>${escapeHtml(existing || '\u2014')}</td>
    <td>${escapeHtml(incoming || '\u2014')}</td>
  </tr>`;
}

function openLibrarySelectedFieldsForMode(mode = 'selected') {
  const result = state.openLibrary.resolvedResult || {};
  const current = openLibraryCurrentMetadataForCompare();
  const fields = [];
  OPEN_LIBRARY_IMPORT_FIELDS.forEach(field => {
    const incoming = openLibraryCleanValue(field.key, result[field.key]);
    if (isOpenLibraryBlank(incoming, field.key)) return;
    if (isMetadataFieldLocked(state.selected || {}, field.payloadKey || field.key)) return;
    if (mode === 'all') { fields.push(field.key); return; }
    if (mode === 'empty') {
      if (isOpenLibraryBlank(current[field.key], field.key)) fields.push(field.key);
      return;
    }
    const checkbox = document.querySelector(`#openLibraryDialog input[data-openlibrary-field="${field.key}"]`);
    if (checkbox?.checked) fields.push(field.key);
  });
  return fields;
}

function openLibraryPayloadFromFields(fieldKeys = []) {
  const result = state.openLibrary.resolvedResult || {};
  const payload = { metadataSource: 'Open Library', metadataStatus: 'Needs Review' };
  fieldKeys.forEach(key => {
    const field = OPEN_LIBRARY_IMPORT_FIELDS.find(f => f.key === key);
    if (!field) return;
    const value = openLibraryCleanValue(key, result[key]);
    if (isOpenLibraryBlank(value, key)) return;
    if (key === 'pageCount') {
      payload.pageCount = Number(value);
      payload.metadataPageCount = Number(value);
    } else if (key === 'language') {
      payload.languageTag = normalizeOpenLibraryLanguage(value);
      payload.language = normalizeOpenLibraryLanguage(value);
    } else if (key === 'publishYear') {
      payload.year = value;
    } else if (key === 'authorWriter') {
      payload.writer = value;
    } else {
      payload[field.payloadKey] = value;
    }
  });
  const currentIsbn = splitIsbnInput($('editIsbn')?.value || combinedIsbnText(state.selected) || '');
  const isbn10 = payload.isbn10 || currentIsbn.isbn10 || state.selected?.isbn10 || '';
  const isbn13 = payload.isbn13 || currentIsbn.isbn13 || state.selected?.isbn13 || '';
  if (payload.isbn10 || payload.isbn13) payload.isbn = [isbn10, isbn13].filter(Boolean).join(' / ');
  return payload;
}

async function importOpenLibraryMetadata(mode = 'selected') {
  const fields = openLibrarySelectedFieldsForMode(mode);
  if (!fields.length) {
    openLibrarySetStatus('No importable Open Library fields were selected.', 'error');
    return;
  }
  const payload = openLibraryPayloadFromFields(fields);
  try {
    openLibrarySetStatus('Importing selected Open Library metadata...', 'info');
    const updated = await saveSelectedMetadata(payload, { tab: 'metadata' });
    if (updated) {
      setStatus('Open Library metadata imported. Cover preview was not imported.');
      closeOpenLibraryDialog();
    }
  } catch (err) {
    console.error('Open Library metadata import failed', err);
    openLibrarySetStatus(`Open Library metadata import failed: ${err?.message || err}`, 'error');
  }
}



const ESRB_IMPORT_FIELDS = [
  { key: 'rating', label: 'ESRB Rating', payloadKey: 'rating' }
];

function esrbArray(value) {
  if (Array.isArray(value)) return value.map(v => String(v || '').trim()).filter(Boolean);
  return String(value || '').split(/[;,|]/).map(v => v.trim()).filter(Boolean);
}

function esrbListLabel(value) {
  return esrbArray(value).join(', ');
}

function esrbCurrentMetadataForCompare() {
  let form = {};
  try { form = buildCurrentMetadataPayloadFromForm(); } catch { form = {}; }
  const item = state.selected || {};
  return { rating: form.rating || item.rating || '' };
}

function esrbInitialSearchValues() {
  let form = {};
  try { form = buildCurrentMetadataPayloadFromForm(); } catch { form = {}; }
  const item = applyClientMetadataOverride(state.selected || {});
  const gameTitle = String(item.gameTitle || item.platformMatchTitle || item.series || item.title || displayTitle(item) || form.gameTitle || form.title || '').trim();
  const platform = String(preferredPlatformOf(item) || detailSystemLabelForItem(item) || item.category || item.system || form.category || '').trim();
  return { q: gameTitle, platform: platform === MULTI_PLATFORM_LABEL ? '' : platform };
}

function esrbResultTitle(result = {}) {
  return [result.title || result.gameTitle, esrbDisplayLabel(result.ratingShort || result.rating), esrbListLabel(result.platforms)].filter(Boolean).join(' - ') || 'ESRB rating result';
}

function ensureEsrbMetadataUi() {
  if (!$('esrbDialog')) {
    const dialog = document.createElement('dialog');
    dialog.id = 'esrbDialog';
    dialog.className = 'openlibrary-dialog esrb-dialog';
    dialog.innerHTML = `
      <div class="openlibrary-modal esrb-modal">
        <header class="openlibrary-modal-head esrb-modal-head">
          <div>
            <h3>Search ESRB Rating</h3>
            <p>Search ESRB.org by game title first, optionally using platform as a hint. Review the existing Guidevault rating beside the ESRB value before importing.</p>
          </div>
          <button type="button" id="esrbCloseBtn" class="ghost tiny">Close</button>
        </header>
        <div id="esrbDialogBody" class="openlibrary-modal-body esrb-modal-body"></div>
      </div>`;
    document.body.appendChild(dialog);
  }

  const existing = $('esrbSearchBtn');
  if (existing) {
    existing.classList.remove('ghost');
    existing.classList.add('metadata-lookup-button', 'esrb-action-button');
    existing.type = 'button';
    existing.textContent = 'ESRB';
    existing.dataset.defaultTitle = 'Search ESRB.org by strategy-guide game title and review the rating before importing it.';
    updateMetadataSourceActionVisibility();
    return;
  }

  const target = $('igdbSearchBtn')?.parentElement
    || $('openLibrarySearchBtn')?.parentElement
    || $('metadataPanel');
  if (target) {
    const btn = document.createElement('button');
    btn.id = 'esrbSearchBtn';
    btn.type = 'button';
    btn.className = 'metadata-lookup-button esrb-action-button';
    btn.textContent = 'ESRB';
    btn.title = 'Search ESRB.org by strategy-guide game title and review the rating before importing it.';
    btn.dataset.defaultTitle = btn.title;
    target.appendChild(btn);
    updateMetadataSourceActionVisibility();
  }
}

function esrbSetStatus(message = '', tone = '') {
  const el = $('esrbStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}

function esrbSearchPanelHtml() {
  const initial = esrbInitialSearchValues();
  const forceInitial = state.esrb?.forceInitialValues || state.esrb?.itemKey !== metadataLookupSelectedItemKey();
  const primaryValue = forceInitial ? (initial.q || '') : ($('esrbSearchInput')?.value || initial.q || '');
  const platformValue = forceInitial ? (initial.platform || '') : ($('esrbPlatformInput')?.value || initial.platform || '');
  return `<section class="openlibrary-search-panel esrb-search-panel">
    <div class="openlibrary-search-fields esrb-search-fields">
      <label>Game title
        <input id="esrbSearchInput" type="search" value="${escapeForAttribute(primaryValue)}" placeholder="Resident Evil 3 Nemesis" />
      </label>
      <label>Platform hint
        <input id="esrbPlatformInput" type="search" value="${escapeForAttribute(platformValue)}" placeholder="Optional: PlayStation, GameCube, Windows" />
      </label>
      <button type="button" id="esrbRunSearchBtn" class="primary">Search</button>
    </div>
    <p class="openlibrary-help">ESRB lookup is separate from IGDB and Open Library. It imports only the Guidevault ESRB Rating field.</p>
    <p id="esrbStatus" class="openlibrary-status esrb-status"></p>
    <div id="esrbResults" class="openlibrary-results esrb-results"></div>
  </section>`;
}

function renderEsrbSearchResults(results = state.esrb.results || []) {
  const host = $('esrbResults');
  if (!host) return;
  if (!results.length) {
    host.innerHTML = '<div class="openlibrary-empty esrb-empty">No ESRB results yet. Search by the actual game title first, then use platform only as a hint.</div>';
    return;
  }
  host.innerHTML = results.map((result, index) => {
    const rating = result.ratingShort || result.rating || '';
    const platforms = esrbListLabel(result.platforms);
    const descriptors = esrbListLabel(result.contentDescriptors);
    const interactive = esrbListLabel(result.interactiveElements);
    const facts = [
      metadataLookupFactHtml('Rating', esrbDisplayLabel(rating)),
      metadataLookupFactHtml('Systems', platforms, 'wide systems'),
      metadataLookupFactHtml('Publisher', result.publisher),
      metadataLookupFactHtml('Content', descriptors, 'wide'),
      metadataLookupFactHtml('Interactive', interactive, 'wide')
    ].join('');
    return `<article class="openlibrary-result-card esrb-result-card metadata-lookup-detailed-card">
      <div class="openlibrary-result-cover esrb-result-badge">${rating ? `<img src="${escapeForAttribute(esrbIconUrl(rating))}" alt="${escapeForAttribute(esrbDisplayLabel(rating))}" loading="lazy" />` : '<span>No rating</span>'}</div>
      <div class="openlibrary-result-main esrb-result-main">
        <h4>${escapeHtml(result.title || 'Untitled')}</h4>
        <p class="metadata-lookup-card-subtitle">${escapeHtml([result.publisher, esrbDisplayLabel(rating)].filter(Boolean).join(' - ') || 'No extra result details')}</p>
        <div class="metadata-lookup-facts">${facts || metadataLookupFactHtml('Systems', 'No platform list returned', 'wide muted')}</div>
        <small>${escapeHtml(result.matchBy || 'Game title')} match - ${escapeHtml(result.confidence || 'Unknown')} confidence</small>
      </div>
      <button type="button" class="ghost esrb-select-result" data-result-index="${index}">Select</button>
    </article>`;
  }).join('');
}

function renderEsrbDialog(step = state.esrb.step || 'search') {
  ensureEsrbMetadataUi();
  const body = $('esrbDialogBody');
  if (!body) return;
  state.esrb.step = step;
  if (step === 'compare' && state.esrb.resolvedResult) {
    body.innerHTML = esrbComparisonHtml(state.esrb.resolvedResult);
    return;
  }
  body.innerHTML = esrbSearchPanelHtml();
  renderEsrbSearchResults(state.esrb.results || []);
}

async function openEsrbMetadataDialog() {
  if (!state.selected) return;
  ensureEsrbMetadataUi();
  resetIndividualMetadataLookupState('esrb', state.selected, true);
  renderEsrbDialog('search');
  state.esrb.forceInitialValues = false;
  const dialog = $('esrbDialog');
  try {
    if (dialog && !dialog.open) dialog.showModal();
  } catch {
    if (dialog) dialog.setAttribute('open', 'open');
  }
  await runEsrbMetadataSearch(true);
}

function closeEsrbDialog() {
  const dialog = $('esrbDialog');
  if (!dialog) return;
  try { dialog.close(); } catch { dialog.removeAttribute('open'); }
}

async function runEsrbMetadataSearch(useInitial = false) {
  ensureEsrbMetadataUi();
  const initial = esrbInitialSearchValues();
  const input = $('esrbSearchInput');
  const platformInput = $('esrbPlatformInput');
  const primary = String((useInitial ? initial.q : input?.value) || initial.q || '').trim();
  const platform = String((useInitial ? initial.platform : platformInput?.value) || initial.platform || '').trim();
  if (!primary) {
    esrbSetStatus('Enter a game title to search ESRB.', 'error');
    return;
  }
  if (input && !input.value) input.value = primary;
  if (platformInput && !platformInput.value) platformInput.value = platform;
  esrbSetStatus('Searching ESRB.org...', 'info');
  const params = new URLSearchParams({ q: primary, limit: '12' });
  if (platform) params.set('platform', platform);
  try {
    const res = await fetch(`/api/esrb/search?${params.toString()}`, { cache: 'no-store' });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || `ESRB search failed. HTTP ${res.status}`);
    state.esrb.results = Array.isArray(data?.results) ? data.results : [];
    renderEsrbSearchResults(state.esrb.results);
    esrbSetStatus(state.esrb.results.length ? `Found ${state.esrb.results.length} result(s). Select the closest rating to compare.` : 'No ESRB results found. Try the exact game title without guide/book wording.', state.esrb.results.length ? 'success' : 'error');
  } catch (err) {
    console.error('ESRB search failed', err);
    esrbSetStatus(`ESRB search failed: ${err?.message || err}`, 'error');
  }
}

async function selectEsrbSearchResult(index) {
  const result = state.esrb.results?.[Number(index)];
  if (!result) return;
  state.esrb.selectedResult = result;
  esrbSetStatus(`Loading details for ${esrbResultTitle(result)}...`, 'info');
  try {
    const res = await fetch('/api/esrb/resolve', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(result)
    });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || `ESRB detail lookup failed. HTTP ${res.status}`);
    state.esrb.resolvedResult = data;
    renderEsrbDialog('compare');
  } catch (err) {
    console.error('ESRB detail lookup failed', err);
    esrbSetStatus(`ESRB detail lookup failed: ${err?.message || err}`, 'error');
  }
}

function esrbComparisonHtml(result = {}) {
  const current = esrbCurrentMetadataForCompare();
  const incomingRating = result.ratingShort || result.rating || '';
  const rows = ESRB_IMPORT_FIELDS.map(field => esrbComparisonRowHtml(field, current, { rating: incomingRating })).join('');
  return `<section class="openlibrary-compare-panel esrb-compare-panel">
    <button type="button" id="esrbBackToResultsBtn" class="ghost tiny">Back to results</button>
    <div class="openlibrary-cover-compare esrb-cover-compare">
      <figure><figcaption>ESRB Rating Badge</figcaption>${incomingRating ? `<img class="esrb-compare-badge" src="${escapeForAttribute(esrbIconUrl(incomingRating))}" alt="${escapeForAttribute(esrbDisplayLabel(incomingRating))}" />` : '<span>No ESRB rating badge</span>'}</figure>
    </div>
    <div class="openlibrary-selected-source esrb-selected-source">
      <h4>${escapeHtml(result.title || 'Selected ESRB result')}</h4>
      <p>${escapeHtml([result.publisher, esrbDisplayLabel(incomingRating), esrbListLabel(result.platforms)].filter(Boolean).join(' - ') || 'Review the rating before importing.')}</p>
      <div class="esrb-detail-grid">
        <div><span>Content Descriptors</span><strong>${escapeHtml(esrbListLabel(result.contentDescriptors) || '\u2014')}</strong></div>
        <div><span>Interactive Elements</span><strong>${escapeHtml(esrbListLabel(result.interactiveElements) || '\u2014')}</strong></div>
      </div>
      ${result.ratingSummary ? `<p class="esrb-rating-summary">${escapeHtml(result.ratingSummary)}</p>` : '<p class="sub">No ESRB rating summary was returned.</p>'}
    </div>
    <div class="openlibrary-table-wrap esrb-table-wrap">
      <table class="openlibrary-comparison-table esrb-comparison-table">
        <thead><tr><th>Import</th><th>Field</th><th>Existing Guidevault Value</th><th>ESRB Value</th></tr></thead>
        <tbody>${rows}</tbody>
      </table>
    </div>
    <p id="esrbStatus" class="openlibrary-status esrb-status"></p>
    <footer class="openlibrary-import-actions esrb-import-actions">
      <button type="button" id="esrbImportSelectedBtn" class="primary">Import Selected Fields</button>
      <button type="button" id="esrbImportEmptyBtn" class="ghost">Import Empty Fields Only</button>
      <button type="button" id="esrbImportAllBtn" class="ghost">Import All Fields</button>
      <button type="button" id="esrbCancelBtn" class="ghost">Cancel</button>
    </footer>
  </section>`;
}

function esrbComparisonRowHtml(field, current, proposed) {
  const existing = String(current[field.key] || '').trim();
  const incoming = String(proposed[field.key] || '').trim();
  const hasIncoming = !!incoming;
  const existingBlank = !existing;
  const existingDisplay = existing ? esrbDisplayLabel(existing) : '\u2014';
  const incomingDisplay = incoming ? esrbDisplayLabel(incoming) : '\u2014';
  const differs = hasIncoming && normalizeEsrbRating(existing) !== normalizeEsrbRating(incoming);
  const locked = isMetadataFieldLocked(state.selected || {}, field.payloadKey || field.key);
  const checked = hasIncoming && existingBlank && !locked ? 'checked' : '';
  const disabled = hasIncoming && !locked ? '' : 'disabled';
  return `<tr class="${differs ? 'different' : ''} ${!hasIncoming ? 'missing-incoming' : ''} ${locked ? 'locked-field' : ''}">
    <td><input type="checkbox" data-esrb-field="${escapeForAttribute(field.key)}" ${checked} ${disabled} /></td>
    <td>${locked ? '<span class="metadata-lock-inline" title="Locked field - skipped by imports">\uD83D\uDD12</span> ' : ''}${escapeHtml(field.label)}</td>
    <td>${escapeHtml(existingDisplay)}</td>
    <td>${escapeHtml(incomingDisplay)}</td>
  </tr>`;
}

function esrbSelectedFieldsForMode(mode = 'selected') {
  const result = state.esrb.resolvedResult || {};
  const current = esrbCurrentMetadataForCompare();
  const incoming = result.ratingShort || result.rating || '';
  const fields = [];
  ESRB_IMPORT_FIELDS.forEach(field => {
    if (!incoming) return;
    if (isMetadataFieldLocked(state.selected || {}, field.payloadKey || field.key)) return;
    if (mode === 'all') { fields.push(field.key); return; }
    if (mode === 'empty') {
      if (!String(current[field.key] || '').trim()) fields.push(field.key);
      return;
    }
    const checkbox = document.querySelector(`#esrbDialog input[data-esrb-field="${field.key}"]`);
    if (checkbox?.checked) fields.push(field.key);
  });
  return fields;
}

function esrbPayloadFromFields(fieldKeys = []) {
  const result = state.esrb.resolvedResult || {};
  const payload = { metadataSource: 'ESRB', metadataStatus: 'Needs Review' };
  const rating = String(result.ratingShort || result.rating || '').trim();
  if (fieldKeys.includes('rating') && rating) payload.rating = rating;
  if (result.sourceUrl) payload.esrbUrl = result.sourceUrl;
  if (result.id) payload.esrbId = String(result.id);
  return payload;
}

async function importEsrbMetadata(mode = 'selected') {
  const fields = esrbSelectedFieldsForMode(mode);
  if (!fields.length) {
    esrbSetStatus('No importable ESRB fields were selected.', 'error');
    return;
  }
  const payload = esrbPayloadFromFields(fields);
  try {
    esrbSetStatus('Importing selected ESRB rating...', 'info');
    const updated = await saveSelectedMetadata(payload, { tab: 'metadata' });
    if (updated) {
      setStatus('ESRB rating imported. IGDB game metadata and Open Library book metadata remain separate.');
      closeEsrbDialog();
    }
  } catch (err) {
    console.error('ESRB rating import failed', err);
    esrbSetStatus(`ESRB rating import failed: ${err?.message || err}`, 'error');
  }
}

const IGDB_IMPORT_FIELDS = [
  { key: 'gameTitle', label: 'Game Title', payloadKey: 'gameTitle' },
  { key: 'gameDeveloper', label: 'Game Developer', payloadKey: 'developer' },
  { key: 'gamePublisher', label: 'Game Publisher', payloadKey: 'gamePublisher' },
  { key: 'gameReleaseYear', label: 'Game Release Year', payloadKey: 'gameReleaseYear' },
  { key: 'gameFranchise', label: 'Game Franchise / Series', payloadKey: 'franchise' },
  { key: 'genre', label: 'Genre', payloadKey: 'genre' },
  { key: 'associatedPlatforms', label: 'Associated Platforms', payloadKey: 'associatedPlatforms' },
  { key: 'preferredPlatform', label: 'Preferred Platform', payloadKey: 'category' }
];

function igdbArray(value) {
  if (Array.isArray(value)) return value.map(v => String(v || '').trim()).filter(Boolean);
  const text = String(value || '').trim();
  if (!text || text === '\u2014') return [];
  return text.split(',').map(v => v.trim()).filter(Boolean);
}

function igdbListLabel(value) {
  return igdbArray(value).join(', ');
}

function metadataLookupFactHtml(label, value, extraClass = '') {
  const text = Array.isArray(value)
    ? value.map(v => String(v || '').trim()).filter(Boolean).join(', ')
    : String(value ?? '').trim();
  if (!text || text === '\u2014') return '';
  const className = ['metadata-lookup-fact', extraClass].filter(Boolean).join(' ');
  return `<div class="${escapeForAttribute(className)}"><span>${escapeHtml(label)}</span><strong title="${escapeForAttribute(text)}">${escapeHtml(text)}</strong></div>`;
}

function isIgdbBlank(value, key = '') {
  if (Array.isArray(value)) return !value.length;
  const text = key === 'associatedPlatforms' ? igdbListLabel(value) : String(value ?? '').trim();
  return !text || text === '\u2014' || text.toLowerCase() === 'unknown';
}

function igdbCleanValue(key, value) {
  if (key === 'associatedPlatforms') return igdbArray(value);
  return String(value ?? '').trim();
}

function igdbDisplayValue(key, value) {
  if (key === 'associatedPlatforms') return igdbListLabel(value);
  return String(value ?? '').trim();
}

function igdbCurrentMetadataForCompare() {
  let form = {};
  try { form = buildCurrentMetadataPayloadFromForm(); } catch { form = {}; }
  const item = state.selected || {};
  const associated = igdbArray($('editAssociatedPlatforms')?.value || platformListText(item));
  return {
    gameTitle: form.gameTitle || item.gameTitle || item.platformMatchTitle || item.series || '',
    gameDeveloper: form.developer || item.developer || item.gameDeveloper || '',
    gamePublisher: form.gamePublisher || item.gamePublisher || '',
    gameReleaseYear: form.gameReleaseYear || item.gameReleaseYear || '',
    gameFranchise: form.franchise || item.franchise || item.series || '',
    genre: form.genre || item.genre || '',
    associatedPlatforms: associated,
    preferredPlatform: form.category || preferredPlatformOf(item) || categoryOf(item) || ''
  };
}

function igdbInitialSearchValues() {
  let form = {};
  try { form = buildCurrentMetadataPayloadFromForm(); } catch { form = {}; }
  const item = applyClientMetadataOverride(state.selected || {});
  const gameTitle = String(item.gameTitle || item.platformMatchTitle || item.series || form.gameTitle || '').trim();
  const fallbackTitle = String(item.title || displayTitle(item) || form.title || '').trim();
  const rawPlatform = String(preferredPlatformOf(item) || categoryOf(item) || form.category || '').trim();
  const platform = rawPlatform === MULTI_PLATFORM_LABEL ? '' : rawPlatform;
  const year = String(item.gameReleaseYear || item.year || form.gameReleaseYear || form.year || '').trim();
  return {
    q: gameTitle || fallbackTitle,
    platform,
    year
  };
}

function igdbResultTitle(result = {}) {
  return [result.gameTitle || result.name, igdbListLabel(result.platforms || result.associatedPlatforms), result.gameReleaseYear].filter(Boolean).join(' - ') || 'IGDB game result';
}

function ensureIgdbMetadataUi() {
  if (!$('igdbDialog')) {
    const dialog = document.createElement('dialog');
    dialog.id = 'igdbDialog';
    dialog.className = 'openlibrary-dialog igdb-dialog';
    dialog.innerHTML = `
      <div class="openlibrary-modal igdb-modal">
        <header class="openlibrary-modal-head igdb-modal-head">
          <div>
            <h3>Search IGDB Game Metadata</h3>
            <p>Search by game title first. Use platform and year as secondary hints. Review existing Guidevault game fields beside IGDB values before importing.</p>
          </div>
          <button type="button" id="igdbCloseBtn" class="ghost tiny">Close</button>
        </header>
        <div id="igdbDialogBody" class="openlibrary-modal-body igdb-modal-body"></div>
      </div>`;
    document.body.appendChild(dialog);
  }

  const existing = $('igdbSearchBtn');
  if (existing) {
    existing.classList.remove('ghost');
    existing.classList.add('metadata-lookup-button', 'igdb-action-button');
    existing.type = 'button';
    existing.textContent = 'IGDB';
    existing.dataset.defaultTitle = 'Search IGDB by strategy-guide game title and review game metadata before importing selected fields.';
    updateMetadataSourceActionVisibility();
    return;
  }

  const target = $('openLibrarySearchBtn')?.parentElement
    || $('exportGuideMetadataBtn')?.parentElement
    || $('metadataPanel');
  if (target) {
    const btn = document.createElement('button');
    btn.id = 'igdbSearchBtn';
    btn.type = 'button';
    btn.className = 'metadata-lookup-button igdb-action-button';
    btn.textContent = 'IGDB';
    btn.title = 'Search IGDB by strategy-guide game title and review game metadata before importing selected fields.';
    btn.dataset.defaultTitle = btn.title;
    target.appendChild(btn);
    updateMetadataSourceActionVisibility();
  }
}

function igdbSetStatus(message = '', tone = '') {
  const el = $('igdbStatus');
  if (!el) return;
  el.textContent = message;
  el.dataset.tone = tone || '';
}

function igdbSearchPanelHtml() {
  const initial = igdbInitialSearchValues();
  const forceInitial = state.igdb?.forceInitialValues || state.igdb?.itemKey !== metadataLookupSelectedItemKey();
  const primaryValue = forceInitial ? (initial.q || '') : ($('igdbSearchInput')?.value || initial.q || '');
  const platformValue = forceInitial ? (initial.platform || '') : ($('igdbPlatformInput')?.value || initial.platform || '');
  const yearValue = forceInitial ? (initial.year || '') : ($('igdbYearInput')?.value || initial.year || '');
  return `<section class="openlibrary-search-panel igdb-search-panel">
    <div class="openlibrary-search-fields igdb-search-fields">
      <label>Game title / primary search
        <input id="igdbSearchInput" type="search" value="${escapeForAttribute(primaryValue)}" placeholder="Final Fantasy III" />
      </label>
      <label>Platform hint
        <input id="igdbPlatformInput" type="search" value="${escapeForAttribute(platformValue)}" placeholder="Optional: SNES, PlayStation, PC" />
      </label>
      <label>Year hint
        <input id="igdbYearInput" type="search" value="${escapeForAttribute(yearValue)}" placeholder="Optional" />
      </label>
      <button type="button" id="igdbRunSearchBtn" class="primary">Search</button>
    </div>
    <p class="openlibrary-help">IGDB requires a Twitch/IGDB Client ID and Client Secret in Settings &gt; Server &gt; Integrations &gt; IGDB. Guidevault keeps this lookup separate from Open Library book metadata.</p>
    <p id="igdbStatus" class="openlibrary-status igdb-status"></p>
    <div id="igdbResults" class="openlibrary-results igdb-results"></div>
  </section>`;
}

function renderIgdbSearchResults(results = state.igdb.results || []) {
  const host = $('igdbResults');
  if (!host) return;
  if (!results.length) {
    host.innerHTML = '<div class="openlibrary-empty igdb-empty">No IGDB results yet. Search by the actual game title first, then use platform/year only as hints.</div>';
    return;
  }
  host.innerHTML = results.map((result, index) => {
    const systems = igdbListLabel(result.associatedPlatforms || result.platforms);
    const studios = [igdbListLabel(result.developers), igdbListLabel(result.publishers)].filter(Boolean).join(' / ');
    const genres = igdbListLabel(result.genres);
    const facts = [
      metadataLookupFactHtml('Systems', systems, 'wide systems'),
      metadataLookupFactHtml('Year', result.gameReleaseYear),
      metadataLookupFactHtml('Developer / Publisher', studios, 'wide'),
      metadataLookupFactHtml('Franchise', result.gameFranchise),
      metadataLookupFactHtml('Genres', genres, 'wide')
    ].join('');
    return `<article class="openlibrary-result-card igdb-result-card metadata-lookup-detailed-card">
      <div class="openlibrary-result-cover igdb-result-cover">${result.coverPreviewUrl ? `<img src="${escapeForAttribute(result.coverPreviewUrl)}" alt="" loading="lazy" />` : '<span>No cover</span>'}</div>
      <div class="openlibrary-result-main igdb-result-main">
        <h4>${escapeHtml(result.gameTitle || result.name || 'Untitled game')}</h4>
        <p class="metadata-lookup-card-subtitle">${escapeHtml(studios || 'No developer/publisher returned')}</p>
        <div class="metadata-lookup-facts">${facts || metadataLookupFactHtml('Systems', 'No systems returned', 'wide muted')}</div>
        <small>${escapeHtml(result.matchBy || 'Game title')} match - ${escapeHtml(result.confidence || 'Unknown')} confidence</small>
      </div>
      <button type="button" class="ghost igdb-select-result" data-result-index="${index}">Select</button>
    </article>`;
  }).join('');
}

function renderIgdbDialog(step = state.igdb.step || 'search') {
  ensureIgdbMetadataUi();
  const body = $('igdbDialogBody');
  if (!body) return;
  state.igdb.step = step;
  if (step === 'compare' && state.igdb.resolvedResult) {
    body.innerHTML = igdbComparisonHtml(state.igdb.resolvedResult);
    return;
  }
  body.innerHTML = igdbSearchPanelHtml();
  renderIgdbSearchResults(state.igdb.results || []);
}

async function openIgdbMetadataDialog() {
  if (!state.selected) return;
  ensureIgdbMetadataUi();
  resetIndividualMetadataLookupState('igdb', state.selected, true);
  renderIgdbDialog('search');
  state.igdb.forceInitialValues = false;
  const dialog = $('igdbDialog');
  try {
    if (dialog && !dialog.open) dialog.showModal();
  } catch {
    if (dialog) dialog.setAttribute('open', 'open');
  }
  await runIgdbMetadataSearch(true);
}

function closeIgdbDialog() {
  const dialog = $('igdbDialog');
  if (!dialog) return;
  try { dialog.close(); } catch { dialog.removeAttribute('open'); }
}

async function runIgdbMetadataSearch(useInitial = false) {
  ensureIgdbMetadataUi();
  const initial = igdbInitialSearchValues();
  const input = $('igdbSearchInput');
  const platformInput = $('igdbPlatformInput');
  const yearInput = $('igdbYearInput');
  const primary = String((useInitial ? initial.q : input?.value) || initial.q || '').trim();
  const platform = String((useInitial ? initial.platform : platformInput?.value) || initial.platform || '').trim();
  const year = String((useInitial ? initial.year : yearInput?.value) || initial.year || '').trim();
  if (!primary) {
    igdbSetStatus('Enter a game title to search IGDB.', 'error');
    return;
  }
  if (input && !input.value) input.value = primary;
  if (platformInput && !platformInput.value) platformInput.value = platform;
  if (yearInput && !yearInput.value) yearInput.value = year;
  igdbSetStatus('Searching IGDB...', 'info');
  const params = new URLSearchParams({ q: primary, limit: '16' });
  if (platform) params.set('platform', platform);
  if (year) params.set('year', year);
  try {
    const res = await fetch(`/api/igdb/search?${params.toString()}`, { cache: 'no-store' });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || `IGDB search failed. HTTP ${res.status}`);
    state.igdb.results = Array.isArray(data?.results) ? data.results : [];
    renderIgdbSearchResults(state.igdb.results);
    igdbSetStatus(state.igdb.results.length ? `Found ${state.igdb.results.length} result(s). Select the closest game to compare fields.` : 'No IGDB results found. Try the exact game title without guide/book wording.', state.igdb.results.length ? 'success' : 'error');
  } catch (err) {
    console.error('IGDB search failed', err);
    igdbSetStatus(`IGDB search failed: ${err?.message || err}`, 'error');
  }
}

async function selectIgdbSearchResult(index) {
  const result = state.igdb.results?.[Number(index)];
  if (!result) return;
  state.igdb.selectedResult = result;
  igdbSetStatus(`Loading details for ${igdbResultTitle(result)}...`, 'info');
  try {
    const res = await fetch('/api/igdb/resolve', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(result)
    });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || `IGDB detail lookup failed. HTTP ${res.status}`);
    state.igdb.resolvedResult = data;
    renderIgdbDialog('compare');
  } catch (err) {
    console.error('IGDB detail lookup failed', err);
    igdbSetStatus(`IGDB detail lookup failed: ${err?.message || err}`, 'error');
  }
}

function igdbComparisonHtml(result = {}) {
  const current = igdbCurrentMetadataForCompare();
  const proposed = {
    gameTitle: igdbCleanValue('gameTitle', result.gameTitle || result.name),
    gameDeveloper: igdbListLabel(result.developers),
    gamePublisher: igdbListLabel(result.publishers),
    gameReleaseYear: igdbCleanValue('gameReleaseYear', result.gameReleaseYear),
    gameFranchise: igdbCleanValue('gameFranchise', result.gameFranchise),
    genre: igdbListLabel(result.genres),
    associatedPlatforms: normalizeGuidevaultPlatformList(result.associatedPlatforms),
    preferredPlatform: igdbCleanValue('preferredPlatform', normalizeGuidevaultPlatformName(result.preferredPlatform))
  };
  const currentCover = state.selected ? coverUrl(state.selected) : '';
  const rows = IGDB_IMPORT_FIELDS.map(field => igdbComparisonRowHtml(field, current, proposed)).join('');
  return `<section class="openlibrary-compare-panel igdb-compare-panel">
    <button type="button" id="igdbBackToResultsBtn" class="ghost tiny">Back to results</button>
    <div class="openlibrary-cover-compare igdb-cover-compare">
      <figure><figcaption>Guidevault Cover</figcaption>${currentCover ? `<img src="${escapeForAttribute(currentCover)}" alt="Current Guidevault cover" />` : '<span>No Guidevault cover</span>'}</figure>
      <figure><figcaption>IGDB Cover <em>Preview only - not imported</em></figcaption>${result.coverPreviewUrl ? `<img src="${escapeForAttribute(result.coverPreviewUrl)}" alt="IGDB cover preview" />` : '<span>No IGDB cover</span>'}</figure>
    </div>
    <div class="openlibrary-selected-source igdb-selected-source">
      <h4>${escapeHtml(result.gameTitle || result.name || 'Selected IGDB result')}</h4>
      <p>${escapeHtml([igdbListLabel(result.developers), igdbListLabel(result.publishers), result.gameReleaseYear, igdbListLabel(result.associatedPlatforms)].filter(Boolean).join(' - ') || 'Review fields before importing.')}</p>
    </div>
    <div class="openlibrary-table-wrap igdb-table-wrap">
      <table class="openlibrary-comparison-table igdb-comparison-table">
        <thead><tr><th>Import</th><th>Field</th><th>Existing Guidevault Value</th><th>IGDB Value</th></tr></thead>
        <tbody>${rows}</tbody>
      </table>
    </div>
    <p id="igdbStatus" class="openlibrary-status igdb-status"></p>
    <footer class="openlibrary-import-actions igdb-import-actions">
      <button type="button" id="igdbImportSelectedBtn" class="primary">Import Selected Fields</button>
      <button type="button" id="igdbImportEmptyBtn" class="ghost">Import Empty Fields Only</button>
      <button type="button" id="igdbImportAllBtn" class="ghost">Import All Fields</button>
      <button type="button" id="igdbCancelBtn" class="ghost">Cancel</button>
    </footer>
  </section>`;
}

function igdbComparisonRowHtml(field, current, proposed) {
  const existing = igdbCleanValue(field.key, current[field.key]);
  const incoming = igdbCleanValue(field.key, proposed[field.key]);
  const hasIncoming = !isIgdbBlank(incoming, field.key);
  const existingBlank = isIgdbBlank(existing, field.key);
  const existingDisplay = igdbDisplayValue(field.key, existing);
  const incomingDisplay = igdbDisplayValue(field.key, incoming);
  const differs = hasIncoming && String(existingDisplay || '').trim() !== String(incomingDisplay || '').trim();
  const locked = isMetadataFieldLocked(state.selected || {}, field.payloadKey || field.key);
  const checked = hasIncoming && existingBlank && !locked ? 'checked' : '';
  const disabled = hasIncoming && !locked ? '' : 'disabled';
  return `<tr class="${differs ? 'different' : ''} ${!hasIncoming ? 'missing-incoming' : ''} ${locked ? 'locked-field' : ''}">
    <td><input type="checkbox" data-igdb-field="${escapeForAttribute(field.key)}" ${checked} ${disabled} /></td>
    <td>${locked ? '<span class="metadata-lock-inline" title="Locked field - skipped by imports">\uD83D\uDD12</span> ' : ''}${escapeHtml(field.label)}</td>
    <td>${escapeHtml(existingDisplay || '\u2014')}</td>
    <td>${escapeHtml(incomingDisplay || '\u2014')}</td>
  </tr>`;
}

function igdbSelectedFieldsForMode(mode = 'selected') {
  const result = state.igdb.resolvedResult || {};
  const current = igdbCurrentMetadataForCompare();
  const proposed = {
    gameTitle: result.gameTitle || result.name,
    gameDeveloper: igdbListLabel(result.developers),
    gamePublisher: igdbListLabel(result.publishers),
    gameReleaseYear: result.gameReleaseYear,
    gameFranchise: result.gameFranchise,
    genre: igdbListLabel(result.genres),
    associatedPlatforms: normalizeGuidevaultPlatformList(result.associatedPlatforms),
    preferredPlatform: normalizeGuidevaultPlatformName(result.preferredPlatform)
  };
  const fields = [];
  IGDB_IMPORT_FIELDS.forEach(field => {
    const incoming = igdbCleanValue(field.key, proposed[field.key]);
    if (isIgdbBlank(incoming, field.key)) return;
    if (isMetadataFieldLocked(state.selected || {}, field.payloadKey || field.key)) return;
    if (mode === 'all') { fields.push(field.key); return; }
    if (mode === 'empty') {
      if (isIgdbBlank(current[field.key], field.key)) fields.push(field.key);
      return;
    }
    const checkbox = document.querySelector(`#igdbDialog input[data-igdb-field="${field.key}"]`);
    if (checkbox?.checked) fields.push(field.key);
  });
  return fields;
}

function igdbPayloadFromFields(fieldKeys = []) {
  const result = state.igdb.resolvedResult || {};
  const payload = { metadataSource: 'IGDB', metadataStatus: 'Needs Review' };
  fieldKeys.forEach(key => {
    const value = key === 'associatedPlatforms'
      ? normalizeGuidevaultPlatformList(result.associatedPlatforms)
      : key === 'gameDeveloper'
        ? igdbListLabel(result.developers)
        : key === 'gamePublisher'
          ? igdbListLabel(result.publishers)
          : key === 'genre'
            ? igdbListLabel(result.genres)
            : String(key === 'preferredPlatform' ? normalizeGuidevaultPlatformName(result[key]) : (result[key] || (key === 'gameTitle' ? result.name : '') || '')).trim();
    if (isIgdbBlank(value, key)) return;
    if (key === 'associatedPlatforms') {
      payload.associatedPlatforms = value;
      payload.coveredPlatforms = value;
      return;
    }
    if (key === 'preferredPlatform') {
      payload.category = value;
      payload.system = value;
      payload.preferredPlatform = value;
      return;
    }
    if (key === 'gameTitle') {
      payload.gameTitle = value;
      payload.platformMatchTitle = value;
      return;
    }
    if (key === 'gameDeveloper') {
      payload.developer = value;
      payload.gameDeveloper = value;
      return;
    }
    if (key === 'gameFranchise') {
      payload.franchise = value;
      payload.gameFranchise = value;
      return;
    }
    const field = IGDB_IMPORT_FIELDS.find(f => f.key === key);
    if (field) payload[field.payloadKey] = value;
  });
  if (result.id) {
    payload.igdbId = String(result.id);
    payload.igdbUrl = result.sourceUrl || '';
  }
  return payload;
}

async function importIgdbMetadata(mode = 'selected') {
  const fields = igdbSelectedFieldsForMode(mode);
  if (!fields.length) {
    igdbSetStatus('No importable IGDB fields were selected.', 'error');
    return;
  }
  const payload = igdbPayloadFromFields(fields);
  try {
    igdbSetStatus('Importing selected IGDB game metadata...', 'info');
    const updated = await saveSelectedMetadata(payload, { tab: 'metadata' });
    if (updated) {
      setStatus('IGDB game metadata imported. Open Library book metadata remains separate. Covers were not imported.');
      closeIgdbDialog();
    }
  } catch (err) {
    console.error('IGDB metadata import failed', err);
    igdbSetStatus(`IGDB metadata import failed: ${err?.message || err}`, 'error');
  }
}

function updateMetadataFileMaintenance() {
  ensureEsrbMetadataUi();
  updateMetadataSourceActionVisibility();
  const panel = $('metadataFileMaintenance');
  if (!panel) return;
  hydrateFileRenameSchema();
  const item = state.selected ? applyClientMetadataOverride(state.selected) : null;
  const currentEl = $('metadataCurrentFileName');
  const suggestedEl = $('metadataSuggestedFileName');
  const renameBtn = $('renameToSuggestedFileNameBtn');
  const statusEl = $('fileRenameStatus');

  if (!item) {
    panel.classList.add('hidden');
    if (currentEl) currentEl.textContent = '-';
    if (suggestedEl) suggestedEl.textContent = '-';
    if (renameBtn) renameBtn.disabled = true;
    if (statusEl) statusEl.textContent = 'Select an item to preview its filename.';
    return;
  }

  let metadata = {};
  try { metadata = buildCurrentMetadataPayloadFromForm(); } catch { metadata = {}; }
  const schema = fileRenameCurrentSchema();
  const currentFileName = metadataExportSourceFileName(item);
  const suggestedFileName = metadataExportSuggestedFileName(metadata, item, schema);
  const sameName = currentFileName && suggestedFileName && currentFileName.toLowerCase() === suggestedFileName.toLowerCase();

  panel.classList.remove('hidden');
  if (currentEl) currentEl.textContent = currentFileName || '-';
  if (suggestedEl) suggestedEl.textContent = suggestedFileName || '-';
  if (renameBtn) {
    renameBtn.disabled = !suggestedFileName || sameName;
    renameBtn.title = sameName
      ? 'The source file already matches the Guide Title filename.'
      : 'Rename the active source file and update the Guidevault index/database entry.';
  }
  if (statusEl) {
    statusEl.classList.remove('error');
    statusEl.textContent = sameName
      ? 'The current source file already matches this naming schema.'
      : 'Guidevault will rename the source file, update the indexed database entry, and keep this same item identity.';
  }
}

function resetFileRenameSchema() {
  saveFileRenameSchema(GUIDEVAULT_DEFAULT_FILENAME_SCHEMA);
  updateMetadataFileMaintenance();
  setStatus('Naming schema reset.');
}

async function renameSelectedFileToSuggestedName() {
  if (!state.selected) return;
  const selectedId = String(state.selected.id || state.selected.Id || '').trim();
  if (!selectedId) return;

  const btn = $('renameToSuggestedFileNameBtn');
  const originalText = btn?.textContent || 'Rename file';
  const schema = saveFileRenameSchema(fileRenameCurrentSchema());
  const metadata = buildCurrentMetadataPayloadFromForm({ metadataSource: 'Manual edit', namingSchema: schema });
  const currentFileName = metadataExportSourceFileName(state.selected);
  const suggestedFileName = metadataExportSuggestedFileName(metadata, state.selected, schema);
  if (!suggestedFileName) return;
  // Send the exact filename shown in the preview so the backend does not re-resolve
  // the schema differently or fall back to a filesystem short-name alias.
  metadata.targetFileName = suggestedFileName;

  const confirmed = await showAppConfirm({
    title: 'Rename file?',
    message: `Current file:
${currentFileName || '-'}

New filename:
${suggestedFileName}

Guidevault will rename the source file, update the indexed database entry, and keep this same item identity.`,
    okText: 'Rename file',
    cancelText: 'Cancel'
  });
  if (!confirmed) return;

  try {
    if (btn) {
      btn.disabled = true;
      btn.textContent = 'Renaming...';
    }

    const res = await fetch(`/api/items/${encodeURIComponent(selectedId)}/file/rename-to-suggested`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(metadata)
    });

    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || data?.message || `Rename failed. HTTP ${res.status}`);

    if (data?.item) {
      replaceItemInState(data.item);
      state.selected = data.item;
      rememberClientMetadataOverride(selectedId, metadata);
      renderDetails(data.item);
      activateTab('library-data');
      applyFilters();
    }

    setStatus(data?.message || `Renamed source file to ${data?.newFileName || suggestedFileName}.`);
    if (btn) {
      btn.textContent = data?.renamed === false ? 'Already renamed' : 'Renamed';
      window.setTimeout(() => { if (btn) btn.textContent = originalText; updateMetadataFileMaintenance(); }, 1200);
    }
  } catch (err) {
    console.error('Guide Title filename rename failed', err);
    const message = `Unable to rename file: ${err?.message || err}`;
    const statusEl = $('fileRenameStatus');
    if (statusEl) {
      statusEl.textContent = message;
      statusEl.classList.add('error');
    }
    setStatus(message);
    if (btn) btn.textContent = 'Rename Failed';
  } finally {
    if (btn) {
      window.setTimeout(() => {
        btn.disabled = false;
        if (btn.textContent === 'Rename Failed') btn.textContent = originalText;
        updateMetadataFileMaintenance();
      }, 900);
    }
  }
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
  if (clone.metadataStatus !== undefined) clone.metadataStatus = normalizeMetadataStatus(clone.metadataStatus);
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
function clearClientMetadataOverride(itemOrId) {
  const key = metadataOverrideKey(itemOrId);
  if (!key) return;
  const map = readClientMetadataOverrides();
  if (Object.prototype.hasOwnProperty.call(map, key)) {
    delete map[key];
    writeClientMetadataOverrides(map);
  }
}


function applyClientMetadataOverride(item, overrides = null) {
  if (!item) return item;
  const key = metadataOverrideKey(item);
  if (!key) return item;
  const map = overrides || readClientMetadataOverrides();
  const override = map[key];
  if (!override) return item;
  const { savedAt, ...metadata } = override;
  return mergeSavedMetadataClientSide(item, {}, metadata);
}

function applyClientMetadataOverridesToLibrary(items = state.items) {
  const list = Array.isArray(items) ? items : [];
  if (!list.length) return list;
  const overrides = readClientMetadataOverrides();
  if (!overrides || !Object.keys(overrides).length) return list;
  const updated = list.map(item => applyClientMetadataOverride(item, overrides));
  if (items === state.items) state.items = updated;
  if (state.selected) state.selected = applyClientMetadataOverride(state.selected, overrides);
  return updated;
}


const METADATA_MANAGER_DEFAULT_COLUMNS = ['kind','metadataStatus','name','category','associatedPlatforms','series','languageTag','region','year','publisher','topics','metadataSource'];
const METADATA_MANAGER_ARRAY_FIELDS = new Set([
  'tags','associatedPlatforms','featuredGames','featuredPlatforms','specialFeatures','includedExtras',
  'coveredGames','coveredPlatforms','guideTopics','charactersCovered','locationsCovered','includedSections','itemsCovered'
]);
const METADATA_MANAGER_READONLY_COLUMNS = new Set(['kind','metadataSource']);
const METADATA_MANAGER_WIDE_COLUMNS = new Set(['name','category','series','publisher','topics','summary','notes','webLink','associatedPlatforms','featuredGames','featuredPlatforms','specialFeatures','includedExtras','coveredGames','coveredPlatforms','guideTopics','charactersCovered','locationsCovered','includedSections','itemsCovered','platformMatchTitle','platformResolverSource','coverSubject','barcodeUpcIssn']);
const METADATA_MANAGER_ALL_COLUMNS = [
  { key:'kind', label:'Type', description:'Manual, Strategy Guide, or Magazine.' },
  { key:'metadataStatus', label:'Status', description:'Metadata review workflow state.' },
  { key:'name', label:'Entry Title / Name', description:'Display title for the item. For magazines, this is the entry title shown on the issue/cover.' },
  { key:'category', label:'Preferred Platform', description:'Primary/preferred platform used for manuals and strategy guides. Magazine rows leave this blank; use Magazine Title for publication sorting and Primary System for issue platform focus.' },
  { key:'series', label:'Series / Publication', description:'Series, franchise, or magazine publication name.' },
  { key:'languageTag', label:'Language', description:'Full language value, such as English.' },
  { key:'region', label:'Region', description:'Region code such as US, JP, or EU.' },
  { key:'year', label:'Year', description:'Release or publication year.' },
  { key:'pageCount', label:'Page Count', description:'Page count entered for the item.' },
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
  { key:'magazineTitle', label:'Magazine Title', description:'Official magazine publication title. Used to group/sort magazine issues by publication.' },
  { key:'issueNumber', label:'Issue Number', description:'Magazine issue number.' },
  { key:'volume', label:'Volume', description:'Magazine volume value.' },
  { key:'coverDate', label:'Cover Date', description:'Cover date printed on the issue.' },
  { key:'barcodeUpcIssn', label:'Barcode / UPC / ISSN', description:'Magazine barcode, UPC, or ISSN when known.' },
  { key:'publicationDate', label:'Publication Date', description:'Publication/release date.' },
  { key:'platformFocus', label:'Platform / Audience Focus', description:'Magazine platform or audience focus.' },
  { key:'primarySystem', label:'Primary System', description:'Main platform/system this magazine issue is focused on.' },
  { key:'magazineCategory', label:'Magazine Category', description:'Magazine category/classification.' },
  { key:'coverSubject', label:'Cover Subject', description:'Main cover subject or feature.' },
  { key:'featuredGames', label:'Featured Games', description:'Magazine featured games.' },
  { key:'featuredPlatforms', label:'Featured Platforms', description:'Magazine featured platforms.' },
  { key:'specialFeatures', label:'Special Features', description:'Special features, sections, or issue components.' },
  { key:'includedExtras', label:'Physical Extras', description:'Physical extras such as posters, inserts, discs, or maps.' },
  { key:'gameTitle', label:'Game Title', description:'Game title for manuals/guides.' },
  { key:'guideType', label:'Guide Type', description:'Strategy guide type.' },
  { key:'edition', label:'Edition', description:'Edition or print identifier.' },
  { key:'franchise', label:'Game Franchise / Series', description:'Game franchise or series for manuals and guides.' },
  { key:'developer', label:'Game Developer', description:'Developer of the associated game.' },
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

const METADATA_MANAGER_KIND_FILTERS = ['Manual', 'Strategy Guide', 'Magazine'];
const METADATA_MANAGER_AUTO_COLUMNS_BY_KIND = {
  Manual: ['kind','metadataStatus','name','gameTitle','category','series','languageTag','region','year','publisher','rating','manualTitle','manualType','includedSections','includedExtras','controlScheme','itemsCovered','metadataSource'],
  'Strategy Guide': ['kind','metadataStatus','name','gameTitle','category','associatedPlatforms','series','languageTag','region','publisher','writer','publicationDate','isbn10','isbn13','guideType','edition','franchise','gameReleaseYear','genre','coveredGames','coveredPlatforms','guideTopics','metadataSource','platformMatchTitle','platformResolverSource','charactersCovered','locationsCovered','developer','gamePublisher','pageCount','rating','topics'],
  Magazine: ['kind','metadataStatus','name','magazineTitle','issueNumber','volume','coverDate','year','publisher','region','languageTag','platformFocus','primarySystem','magazineCategory','coverSubject','featuredGames','featuredPlatforms','specialFeatures','includedExtras','tags','metadataSource']
};

function metadataManagerSelectedKinds() {
  state.metadataManager = state.metadataManager || {};
  const fromCheckboxes = Array.isArray(state.metadataManager.kindFilters) ? state.metadataManager.kindFilters.filter(kind => METADATA_MANAGER_KIND_FILTERS.includes(kind)) : [];
  if (fromCheckboxes.length) return fromCheckboxes;
  const legacy = String(state.metadataManager.filterKind || '').trim();
  if (METADATA_MANAGER_KIND_FILTERS.includes(legacy)) return [legacy];
  return METADATA_MANAGER_KIND_FILTERS.slice();
}

function metadataManagerSetSelectedKinds(kinds = []) {
  state.metadataManager = state.metadataManager || {};
  const clean = (kinds || []).filter(kind => METADATA_MANAGER_KIND_FILTERS.includes(kind));
  const next = clean.length ? clean : METADATA_MANAGER_KIND_FILTERS.slice();
  const previous = Array.isArray(state.metadataManager.kindFilters) ? state.metadataManager.kindFilters : [];
  const changed = next.length !== previous.length || next.some(kind => !previous.includes(kind));
  state.metadataManager.kindFilters = next;
  state.metadataManager.filterKind = state.metadataManager.kindFilters.length === 1 ? state.metadataManager.kindFilters[0] : '';
  if (changed) state.metadataManager.useCustomColumns = false;
}

function metadataManagerKindSummaryLabel(kinds = metadataManagerSelectedKinds()) {
  const selected = (Array.isArray(kinds) && kinds.length ? kinds : METADATA_MANAGER_KIND_FILTERS).filter(kind => METADATA_MANAGER_KIND_FILTERS.includes(kind));
  if (!selected.length || selected.length === METADATA_MANAGER_KIND_FILTERS.length) return 'Manuals, Strategy Guides, Magazines';
  return selected.map(kind => kind === 'Strategy Guide' ? 'Strategy Guides' : `${kind}s`).join(', ');
}

function syncMetadataManagerKindDropdown() {
  const summary = $('metadataManagerKindSummary');
  if (summary) summary.textContent = metadataManagerKindSummaryLabel();
  const dropdown = $('metadataManagerKindDropdown');
  if (dropdown) dropdown.title = `Showing: ${metadataManagerKindSummaryLabel()}`;
}

function metadataManagerAutoColumnKeysForKinds(kinds = metadataManagerSelectedKinds()) {
  const selected = (kinds && kinds.length ? kinds : METADATA_MANAGER_KIND_FILTERS).filter(kind => METADATA_MANAGER_KIND_FILTERS.includes(kind));
  const keys = [];
  const add = key => { if (!keys.includes(key) && metadataManagerValidColumnKeys().has(key)) keys.push(key); };
  ['kind','metadataStatus','name'].forEach(add);
  selected.forEach(kind => (METADATA_MANAGER_AUTO_COLUMNS_BY_KIND[kind] || []).forEach(add));
  if (selected.length > 1) ['category','series','languageTag','region','year','publisher','topics','metadataSource'].forEach(add);
  return keys.length ? keys : METADATA_MANAGER_DEFAULT_COLUMNS.slice();
}

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
  const title = String(item.title || item.Title || '').trim();
  if (title) return title;
  if (item.kind === 'Magazine') {
    const base = item.magazineTitle || item.series || '';
    const issue = String(item.issueNumber || '').trim();
    return issue && base ? `${base} #${issue}` : base;
  }
  if (item.kind === 'Manual') return item.manualTitle || item.gameTitle || '';
  if (item.kind === 'Strategy Guide') return item.gameTitle || '';
  return '';
}

function metadataManagerCategoryValue(item) {
  if (item?.kind === 'Magazine') return '';
  return preferredPlatformOf(item);
}

function metadataManagerSeriesValue(item) {
  if (item?.kind === 'Magazine') return item.series || item.magazineTitle || '';
  if (item?.kind === 'Strategy Guide' || item?.kind === 'Manual') return item.series || item.franchise || '';
  return item?.series || '';
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


function metadataManagerSearchText(item) {
  if (!item) return '';
  const cacheKey = [
    item.id, item.Id, item.updatedAt, item.modified, item.metadataSource, item.metadataStatus,
    item.title, item.manualTitle, item.gameTitle, item.magazineTitle,
    item.kind, metadataStatusOf(item), item.publisher, item.gamePublisher, item.year,
    item.languageTag, item.region, item.series, item.franchise,
    item.category, item.system, item.primarySystem,
    item.summary
  ].join('|');

  if (item.__metadataManagerSearchKey === cacheKey && item.__metadataManagerSearchText) {
    return item.__metadataManagerSearchText;
  }

  const text = [
    metadataManagerItemName(item),
    item.title,
    item.kind,
    metadataStatusOf(item),
    metadataManagerCategoryValue(item),
    metadataManagerSeriesValue(item),
    item.publisher,
    item.gamePublisher,
    item.year,
    item.languageTag,
    item.region,
    metadataManagerTopicValue(item),
    platformListText(item),
    item.metadataSource,
    item.summary,
    ...(item.tags || [])
  ].join(' ').toLowerCase();

  try {
    Object.defineProperty(item, '__metadataManagerSearchKey', { value: cacheKey, writable: true, configurable: true });
    Object.defineProperty(item, '__metadataManagerSearchText', { value: text, writable: true, configurable: true });
  } catch {
    item.__metadataManagerSearchKey = cacheKey;
    item.__metadataManagerSearchText = text;
  }

  return text;
}

function metadataManagerEditableItems() {
  const allowed = new Set(['Manual', 'Strategy Guide', 'Magazine']);
  const manager = state.metadataManager || {};
  const q = String(manager.search || '').trim().toLowerCase();
  const selectedKinds = new Set(metadataManagerSelectedKinds());
  const statusFilter = normalizeMetadataStatus(manager.statusFilter || '', '');
  const missing = String(manager.missing || '').trim();
  const category = String(manager.category || '').trim().toLowerCase();
  const filtered = (state.items || []).filter(item => {
    if (!allowed.has(item.kind)) return false;
    if (!selectedKinds.has(item.kind)) return false;
    if (statusFilter && metadataStatusOf(item) !== statusFilter) return false;
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
    if (q && !metadataManagerSearchText(item).includes(q)) return false;
    return true;
  });
  return metadataManagerSortItems(filtered);
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
  const reviewed = items.filter(i => metadataStatusOf(i) === 'Reviewed').length;
  const needsReview = items.filter(i => metadataStatusOf(i) === 'Needs Review').length;
  const failedLookup = items.filter(i => metadataStatusOf(i) === 'Failed Lookup').length;
  const edited = Object.keys(state.metadataManager.dirty || {}).length;
  return [
    ['Visible', total],
    ['Reviewed', reviewed],
    ['Needs review', needsReview],
    ['Failed lookup', failedLookup],
    ['Missing language', missingLanguage],
    ['Missing region', missingRegion],
    ['Missing topics', missingTopics],
    ['Unsorted guides', unsortedGuides],
    ['Multiple platforms', multiPlatformGuides],
    ['Edited rows', edited]
  ];
}

function metadataManagerFieldValue(item, field) {
  if (field === 'metadataStatus') return metadataStatusOf(item);
  if (field === 'name') return metadataManagerItemName(item);
  if (field === 'category') return item?.kind === 'Magazine' ? '' : metadataManagerCategoryValue(item);
  if (field === 'series') return metadataManagerSeriesValue(item);
  if (field === 'publisher') return item.publisher || '';
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
      <img src="${coverUrl(item, { width: 320 })}" alt="${escapeForAttribute(title)} cover" onerror="this.onerror=null;this.src='/assets/missing-cover.svg';" />
    </div>
    <div class="metadata-cover-preview-caption">
      <strong>${escapeHtml(title)}</strong>
      <span>${escapeHtml(item.kind || 'Entry')}${year ? ` \u2022 ${escapeHtml(year)}` : ''}</span>
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
    } else if (field === 'category') {
      if (item.kind !== 'Magazine') {
        payload.category = value;
        payload.system = value;
      }
    } else if (field === 'series') {
      payload.series = value;
    } else if (field === 'publisher') {
      payload.publisher = value;
    } else if (field === 'pageCount') {
      const count = Number(value || 0) || 0;
      payload.pageCount = count;
      payload.metadataPageCount = count;
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
  prepareLibraryItemComputedFields(updated);
  state.items[index] = updated;
  markLibraryIndexesDirty();
  rememberClientMetadataOverride(id, payload);
  if (state.selected && metadataManagerItemId(state.selected) === id) state.selected = updated;
  return updated;
}


function metadataManagerValidColumnKeys() {
  return new Set(METADATA_MANAGER_ALL_COLUMNS.map(column => column.key));
}

function metadataManagerColumnDefinition(key) {
  return METADATA_MANAGER_ALL_COLUMNS.find(column => column.key === key) || null;
}

function metadataManagerLoadVisibleColumns() {
  state.metadataManager = state.metadataManager || {};
  const valid = metadataManagerValidColumnKeys();
  if (!state.metadataManager.useCustomColumns) {
    const auto = metadataManagerAutoColumnKeysForKinds();
    state.metadataManager.visibleColumns = auto;
    return auto;
  }
  let saved = [];
  try { saved = JSON.parse(localStorage.getItem(GUIDEVAULT_METADATA_COLUMNS_KEY) || '[]') || []; } catch { saved = []; }
  const requested = (Array.isArray(state.metadataManager?.visibleColumns) && state.metadataManager.visibleColumns.length ? state.metadataManager.visibleColumns : saved)
    .filter(key => valid.has(key));
  const resolved = requested.length ? requested : metadataManagerAutoColumnKeysForKinds();
  state.metadataManager.visibleColumns = resolved;
  return resolved;
}

function metadataManagerSaveVisibleColumns(columns) {
  state.metadataManager = state.metadataManager || {};
  state.metadataManager.useCustomColumns = true;
  const valid = metadataManagerValidColumnKeys();
  const seen = new Set();
  const resolved = (columns || []).filter(key => {
    if (!valid.has(key) || seen.has(key)) return false;
    seen.add(key);
    return true;
  });
  const safe = resolved.length ? resolved : METADATA_MANAGER_DEFAULT_COLUMNS.slice();
  state.metadataManager.visibleColumns = safe;
  try { localStorage.setItem(GUIDEVAULT_METADATA_COLUMNS_KEY, JSON.stringify(safe)); } catch {}
  return safe;
}

function metadataManagerVisibleColumns() {
  return metadataManagerLoadVisibleColumns()
    .map(metadataManagerColumnDefinition)
    .filter(Boolean);
}

function metadataManagerSetColumnVisible(key, visible) {
  const current = metadataManagerLoadVisibleColumns();
  const set = new Set(current);
  if (visible) set.add(key); else set.delete(key);
  const ordered = [
    ...current.filter(existing => set.has(existing)),
    ...METADATA_MANAGER_ALL_COLUMNS.map(column => column.key).filter(existing => set.has(existing) && !current.includes(existing))
  ];
  metadataManagerSaveVisibleColumns(ordered);
  renderMetadataManagerColumnPicker();
  renderMetadataManager();
}

function metadataManagerMoveColumn(key, direction = 0) {
  const current = metadataManagerLoadVisibleColumns();
  const from = current.indexOf(key);
  if (from < 0) return;
  const to = Math.max(0, Math.min(current.length - 1, from + Number(direction || 0)));
  if (from === to) return;
  const next = current.slice();
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  metadataManagerSaveVisibleColumns(next);
  renderMetadataManagerColumnPicker();
  renderMetadataManager();
  metadataManagerSetStatus(`Moved ${metadataManagerColumnDefinition(key)?.label || key} column.`, 'success');
}

function metadataManagerReorderColumn(sourceKey, targetKey) {
  if (!sourceKey || !targetKey || sourceKey === targetKey) return;
  const current = metadataManagerLoadVisibleColumns();
  const from = current.indexOf(sourceKey);
  const to = current.indexOf(targetKey);
  if (from < 0 || to < 0) return;
  const next = current.slice();
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  metadataManagerSaveVisibleColumns(next);
  renderMetadataManagerColumnPicker();
  renderMetadataManager();
  metadataManagerSetStatus(`Moved ${metadataManagerColumnDefinition(sourceKey)?.label || sourceKey} column.`, 'success');
}

function metadataManagerLoadSort() {
  const valid = metadataManagerValidColumnKeys();
  let saved = null;
  try { saved = JSON.parse(localStorage.getItem(GUIDEVAULT_METADATA_COLUMN_SORT_KEY) || 'null'); } catch { saved = null; }
  const current = state.metadataManager || {};
  const currentKey = valid.has(current.sortKey) ? current.sortKey : '';
  const savedKey = valid.has(saved?.key) ? saved.key : '';
  const key = currentKey || savedKey || 'name';
  const direction = current.sortDirection === 'desc' || saved?.direction === 'desc' ? 'desc' : 'asc';
  state.metadataManager.sortKey = key;
  state.metadataManager.sortDirection = direction;
  return { key, direction };
}

function metadataManagerSaveSort(key, direction = 'asc') {
  const valid = metadataManagerValidColumnKeys();
  const safeKey = valid.has(key) ? key : 'name';
  const safeDirection = direction === 'desc' ? 'desc' : 'asc';
  state.metadataManager.sortKey = safeKey;
  state.metadataManager.sortDirection = safeDirection;
  try { localStorage.setItem(GUIDEVAULT_METADATA_COLUMN_SORT_KEY, JSON.stringify({ key: safeKey, direction: safeDirection })); } catch {}
  return { key: safeKey, direction: safeDirection };
}

function metadataManagerSetSort(key) {
  const current = metadataManagerLoadSort();
  const direction = current.key === key && current.direction === 'asc' ? 'desc' : 'asc';
  metadataManagerSaveSort(key, direction);
  renderMetadataManager();
}

function metadataManagerSortValue(item, key) {
  const value = metadataManagerFieldValue(item, key);
  if (key === 'pageCount' || key === 'metadataPageCount') return Number(value || 0) || 0;
  const yearMatch = String(value || '').match(/\d{4}/);
  if (['year','publishYear','gameReleaseYear','coverDate','publicationDate'].includes(key) && yearMatch) return Number(yearMatch[0]);
  if (Array.isArray(value)) return value.join(', ');
  return String(value ?? '').trim().toLowerCase();
}

function metadataManagerSortItems(items) {
  const sort = metadataManagerLoadSort();
  const dir = sort.direction === 'desc' ? -1 : 1;
  return (items || []).slice().sort((a, b) => {
    const av = metadataManagerSortValue(a, sort.key);
    const bv = metadataManagerSortValue(b, sort.key);
    if (typeof av === 'number' || typeof bv === 'number') {
      return ((Number(av) || 0) - (Number(bv) || 0)) * dir || metadataManagerItemName(a).localeCompare(metadataManagerItemName(b), undefined, { sensitivity: 'base' });
    }
    return String(av).localeCompare(String(bv), undefined, { numeric: true, sensitivity: 'base' }) * dir || metadataManagerItemName(a).localeCompare(metadataManagerItemName(b), undefined, { sensitivity: 'base' });
  });
}

function metadataManagerResetColumns() {
  state.metadataManager = state.metadataManager || {};
  state.metadataManager.useCustomColumns = false;
  state.metadataManager.visibleColumns = metadataManagerAutoColumnKeysForKinds();
  try { localStorage.removeItem(GUIDEVAULT_METADATA_COLUMNS_KEY); } catch {}
  renderMetadataManagerColumnPicker();
  renderMetadataManager();
  metadataManagerSetStatus('Metadata columns returned to the automatic content-type view.', 'success');
}

function metadataManagerShowAllColumns() {
  metadataManagerSaveVisibleColumns(METADATA_MANAGER_ALL_COLUMNS.map(column => column.key));
  renderMetadataManagerColumnPicker();
  renderMetadataManager();
  metadataManagerSetStatus('All available metadata columns are visible.', 'success');
}

const METADATA_MANAGER_COLUMN_GROUPS = [
  { key: 'core', label: 'Core / All content' },
  { key: 'book', label: 'Book identifiers / publishing' },
  { key: 'strategy', label: 'Strategy Guide / Game fields' },
  { key: 'manual', label: 'Manual fields' },
  { key: 'magazine', label: 'Magazine fields' },
  { key: 'lookup', label: 'Lookup / source fields' },
  { key: 'notes', label: 'Notes / extras' }
];

function metadataManagerColumnGroupKey(column) {
  const key = String(column?.key || '');
  if (['kind','metadataStatus','name','category','series','languageTag','region','year','publisher','topics','rating'].includes(key)) return 'core';
  if (['asin','isbn10','isbn13','writer','webLink','publicationDate','pageCount'].includes(key)) return 'book';
  if (['gameTitle','guideType','edition','franchise','developer','gamePublisher','gameReleaseYear','genre','associatedPlatforms','coveredGames','coveredPlatforms','guideTopics','charactersCovered','locationsCovered'].includes(key)) return 'strategy';
  if (['manualTitle','manualType','includedSections','controlScheme','itemsCovered','warrantySupport'].includes(key)) return 'manual';
  if (['magazineTitle','issueNumber','volume','coverDate','barcodeUpcIssn','platformFocus','primarySystem','magazineCategory','coverSubject','featuredGames','featuredPlatforms','specialFeatures'].includes(key)) return 'magazine';
  if (['metadataSource','platformMatchTitle','platformResolverSource'].includes(key)) return 'lookup';
  return 'notes';
}

function renderMetadataManagerColumnPicker() {
  const picker = $('metadataManagerColumnPicker');
  if (!picker) return;
  const visible = metadataManagerLoadVisibleColumns();
  const visibleSet = new Set(visible);
  const columnsByGroup = new Map(METADATA_MANAGER_COLUMN_GROUPS.map(group => [group.key, []]));
  METADATA_MANAGER_ALL_COLUMNS.forEach(column => {
    const groupKey = metadataManagerColumnGroupKey(column);
    if (!columnsByGroup.has(groupKey)) columnsByGroup.set(groupKey, []);
    columnsByGroup.get(groupKey).push(column);
  });
  picker.innerHTML = METADATA_MANAGER_COLUMN_GROUPS.map(group => {
    const columns = columnsByGroup.get(group.key) || [];
    if (!columns.length) return '';
    return `<section class="metadata-manager-column-group" aria-label="${escapeForAttribute(group.label)}">
      <h4>${escapeHtml(group.label)}</h4>
      <div class="metadata-manager-column-group-grid">
        ${columns.map(column => {
          const position = visible.indexOf(column.key);
          const isVisible = visibleSet.has(column.key);
          return `<label class="metadata-manager-column-option" title="${escapeForAttribute(column.description || '')}">
            <input type="checkbox" data-column-key="${escapeForAttribute(column.key)}" ${isVisible ? 'checked' : ''} />
            <span>${escapeHtml(column.label)}</span>
            ${isVisible ? `<button class="ghost tiny metadata-column-move" type="button" data-column-move="-1" data-column-key="${escapeForAttribute(column.key)}" ${position <= 0 ? 'disabled' : ''} title="Move left">\u2190</button><button class="ghost tiny metadata-column-move" type="button" data-column-move="1" data-column-key="${escapeForAttribute(column.key)}" ${position === visible.length - 1 ? 'disabled' : ''} title="Move right">\u2192</button>` : ''}
          </label>`;
        }).join('')}
      </div>
    </section>`;
  }).join('');
  const active = $('metadataManagerColumnActiveCount');
  if (active) active.textContent = state.metadataManager?.useCustomColumns ? `${visible.length} of ${METADATA_MANAGER_ALL_COLUMNS.length} custom columns shown - click headers to sort or drag to reorder` : `${visible.length} automatic columns shown for selected content type(s)`;
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
  text = text.replace(/\s*[-\u2013\u2014]\s*/g, ' - ');
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


function metadataManagerDefaultRenderLimit() {
  return METADATA_MANAGER_DEFAULT_RENDER_LIMIT;
}

function metadataManagerCurrentRenderLimit(total = 0) {
  state.metadataManager = state.metadataManager || {};
  const raw = Number(state.metadataManager.renderLimit || 0);
  const fallback = metadataManagerDefaultRenderLimit();
  const safe = Number.isFinite(raw) && raw > 0 ? raw : fallback;
  return Math.max(1, Math.min(Math.max(total, fallback), Math.floor(safe)));
}

function metadataManagerResetRenderLimit() {
  state.metadataManager = state.metadataManager || {};
  state.metadataManager.renderLimit = metadataManagerDefaultRenderLimit();
}

function metadataManagerLoadMoreRows() {
  const items = metadataManagerEditableItems();
  const current = metadataManagerCurrentRenderLimit(items.length);
  state.metadataManager.renderLimit = Math.min(items.length, current + METADATA_MANAGER_RENDER_STEP);
  renderMetadataManager();
}

function metadataManagerShowAllRows() {
  const items = metadataManagerEditableItems();
  state.metadataManager.renderLimit = Math.max(items.length, metadataManagerDefaultRenderLimit());
  renderMetadataManager();
}

function metadataManagerCollapseRows() {
  metadataManagerResetRenderLimit();
  renderMetadataManager();
}

function metadataManagerBatchEditableColumns() {
  return metadataManagerVisibleColumns()
    .filter(column => column && !METADATA_MANAGER_READONLY_COLUMNS.has(column.key));
}

function metadataManagerBatchInputId(key) {
  return `metadataBatchField_${String(key || '').replace(/[^A-Za-z0-9_-]/g, '_')}`;
}

function metadataManagerBatchPlaceholder(column) {
  const key = column?.key || '';
  if (key === 'metadataStatus') return '';
  if (key === 'languageTag') return 'English';
  if (key === 'region') return 'US, JP, EU...';
  if (key === 'category') return 'PlayStation 2';
  if (key === 'publisher') return 'Prima Games, Nintendo...';
  if (key === 'year' || key === 'gameReleaseYear') return '1998';
  if (key === 'publicationDate' || key === 'coverDate') return 'Jul/Aug 1988';
  if (key === 'rating') return 'Everyone, Teen, Not Rated...';
  if (key === 'webLink') return 'https://...';
  if (key === 'pageCount') return '144';
  if (key === 'topics' || METADATA_MANAGER_ARRAY_FIELDS.has(key)) return 'Comma-separated values';
  return column?.label || key;
}

function metadataManagerBatchControlHtml(column) {
  const key = column?.key || '';
  const id = metadataManagerBatchInputId(key);
  const label = column?.label || key;
  const title = column?.description || `Apply ${label} to selected rows.`;
  if (key === 'metadataStatus') {
    return `<label for="${escapeForAttribute(id)}" title="${escapeForAttribute(title)}">${escapeHtml(label)}
      <select id="${escapeForAttribute(id)}" data-metadata-batch-field-control="${escapeForAttribute(key)}">
        <option value="">Do not change</option>
        <option value="Unreviewed">Unreviewed</option>
        <option value="Needs Review">Needs Review</option>
        <option value="Reviewed">Reviewed</option>
        <option value="Locked">Locked</option>
        <option value="Failed Lookup">Failed Lookup</option>
        <option value="Manual Only">Manual Only</option>
      </select>
    </label>`;
  }
  return `<label for="${escapeForAttribute(id)}" title="${escapeForAttribute(title)}">${escapeHtml(label)}
    <input id="${escapeForAttribute(id)}" type="text" data-metadata-batch-field-control="${escapeForAttribute(key)}" placeholder="${escapeForAttribute(metadataManagerBatchPlaceholder(column))}" />
  </label>`;
}

function metadataManagerRenderBatchEditor() {
  const grid = $('metadataManagerBatchGrid');
  if (!grid) return;
  const columns = metadataManagerBatchEditableColumns();
  const activeKeys = columns.map(column => column.key).join('|');
  if (grid.dataset.activeColumnKeys === activeKeys && grid.childElementCount) return;
  const previousValues = {};
  grid.querySelectorAll('[data-metadata-batch-field-control]').forEach(control => {
    previousValues[control.dataset.metadataBatchFieldControl || ''] = control.value;
  });
  const topicModeValue = $('metadataBatchTopicsMode')?.value || 'add';
  grid.dataset.activeColumnKeys = activeKeys;
  if (!columns.length) {
    grid.innerHTML = '<p class="metadata-manager-batch-empty">Show at least one editable column to make it available for batch editing.</p>';
    return;
  }
  grid.innerHTML = columns.map(metadataManagerBatchControlHtml).join('')
    + (columns.some(column => column.key === 'topics')
      ? `<label for="metadataBatchTopicsMode" class="metadata-manager-batch-mode-field">Topics Mode
          <select id="metadataBatchTopicsMode" data-metadata-batch-mode="topics">
            <option value="add">Add to existing</option>
            <option value="replace">Replace existing</option>
            <option value="remove">Remove from existing</option>
          </select>
        </label>`
      : '');
  grid.querySelectorAll('[data-metadata-batch-field-control]').forEach(control => {
    const key = control.dataset.metadataBatchFieldControl || '';
    if (previousValues[key] !== undefined) control.value = previousValues[key];
  });
  const topicMode = $('metadataBatchTopicsMode');
  if (topicMode) topicMode.value = topicModeValue;
}

function metadataManagerBatchEntries() {
  const entries = [];
  document.querySelectorAll('[data-metadata-batch-field-control]').forEach(control => {
    const field = control.dataset.metadataBatchFieldControl || '';
    if (!field || METADATA_MANAGER_READONLY_COLUMNS.has(field)) return;
    let value = control.value;
    if (field === 'metadataStatus') value = normalizeMetadataStatus(value || '', '');
    else value = String(value ?? '').trim();
    if (!value) return;
    entries.push({ field, value });
  });
  return entries;
}

function metadataManagerBatchPayloadForItem(item, entries) {
  const changes = {};
  const payload = { metadataSource: 'Bulk metadata manager' };
  const topicsMode = $('metadataBatchTopicsMode')?.value || 'add';
  entries.forEach(entry => {
    if (!entry?.field) return;
    if (entry.field === 'topics') {
      const topicField = metadataManagerTopicField(item);
      payload[topicField] = mergeTokenLists(item[topicField] || item.tags || [], entry.value, topicsMode);
      return;
    }
    changes[entry.field] = entry.value;
  });
  return { ...metadataManagerRowPayload(item, changes), ...payload };
}


function renderMetadataManager() {
  if (!$('settingsMetadataManagerPanel')) return;
  state.metadataManager = state.metadataManager || { selectedIds: [], dirty: {}, filterKind: '', kindFilters: ['Manual','Strategy Guide','Magazine'], statusFilter: '', search: '', missing: '', category: '', visibleColumns: [], useCustomColumns: false, sortKey: '', sortDirection: 'asc', draggedColumnKey: '', renderLimit: METADATA_MANAGER_DEFAULT_RENDER_LIMIT };
  const categorySelect = $('metadataManagerCategory');
  if (categorySelect && !categorySelect.options.length) metadataManagerRenderCategoryFilter();
  const columnPicker = $('metadataManagerColumnPicker');
  if (columnPicker && !columnPicker.childElementCount) renderMetadataManagerColumnPicker();
  const manager = state.metadataManager;
  if ($('metadataManagerSearch')) $('metadataManagerSearch').value = manager.search || '';
  if ($('metadataManagerKind')) $('metadataManagerKind').value = manager.filterKind || '';
  const selectedKinds = new Set(metadataManagerSelectedKinds());
  document.querySelectorAll('[data-metadata-manager-kind]').forEach(input => { input.checked = selectedKinds.has(input.dataset.metadataManagerKind || ''); });
  syncMetadataManagerKindDropdown();
  if ($('metadataManagerStatusFilter')) $('metadataManagerStatusFilter').value = normalizeMetadataStatus(manager.statusFilter || '', '');
  if ($('metadataManagerMissing')) $('metadataManagerMissing').value = manager.missing || '';
  if ($('metadataManagerCategory')) $('metadataManagerCategory').value = manager.category || '';
  const items = metadataManagerEditableItems();
  const selectedSet = new Set(manager.selectedIds || []);
  const dirty = manager.dirty || {};
  const columns = metadataManagerVisibleColumns();
  metadataManagerRenderBatchEditor();
  const renderLimit = metadataManagerCurrentRenderLimit(items.length);
  const renderedItems = items.slice(0, renderLimit);
  const hiddenCount = Math.max(0, items.length - renderedItems.length);
  const summary = $('metadataManagerSummary');
  if (summary) {
    summary.innerHTML = metadataManagerSummaryStats(items).map(([label, value]) => `<div class="metadata-manager-stat"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></div>`).join('');
  }
  const sort = metadataManagerLoadSort();
  const headerRow = $('metadataManagerHeaderRow');
  if (headerRow) {
    headerRow.innerHTML = `
      <th><input id="metadataManagerHeaderCheck" type="checkbox" aria-label="Select visible rows" /></th>
      ${columns.map(column => `<th class="metadata-manager-column-header ${sort.key === column.key ? 'is-sorted' : ''}" draggable="true" data-column-key="${escapeForAttribute(column.key)}" title="Click to sort. Drag this header to reorder columns."><div class="metadata-manager-column-head"><button class="metadata-manager-sort-button" type="button" data-column-sort="${escapeForAttribute(column.key)}" aria-label="Sort by ${escapeForAttribute(column.label)}"><span>${escapeHtml(column.label)}</span><b>${sort.key === column.key ? (sort.direction === 'desc' ? '\u2193' : '\u2191') : '\u2195'}</b></button><span class="metadata-manager-column-drag-handle" aria-hidden="true" title="Drag to reorder">\u22EE\u22EE</span></div></th>`).join('')}
    `;
  }
  const body = $('metadataManagerTableBody');
  if (body) {
    body.innerHTML = items.length ? renderedItems.map(item => {
      const id = metadataManagerItemId(item);
      const rowDirty = dirty[id] || {};
      const rowClass = Object.keys(rowDirty).length ? ' class="metadata-row-dirty"' : '';
      const rowAssociatedPlatforms = rowDirty.associatedPlatforms !== undefined ? itemArray(rowDirty.associatedPlatforms) : associatedPlatformsOf(item);
      const preferredPlatformReadOnly = item.kind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(rowAssociatedPlatforms);
      const inputField = (name, value, readOnly = false, title = '') => `<input class="metadata-manager-input ${METADATA_MANAGER_WIDE_COLUMNS.has(name) ? 'wide' : ''} ${readOnly ? 'readonly' : ''}" data-id="${escapeForAttribute(id)}" data-field="${escapeForAttribute(name)}" value="${escapeForAttribute(rowDirty[name] ?? value ?? '')}" ${readOnly ? 'readonly aria-readonly="true"' : ''} ${title ? `title="${escapeForAttribute(title)}"` : ''} />`;
      const cellFor = column => {
        if (column.key === 'metadataStatus') {
          const currentStatus = normalizeMetadataStatus(rowDirty.metadataStatus ?? metadataStatusOf(item));
          return `<select class="metadata-manager-input metadata-status-select" data-id="${escapeForAttribute(id)}" data-field="metadataStatus">${metadataStatusOptionsHtml(currentStatus)}</select>`;
        }
        if (column.key === 'kind') return `<span class="metadata-kind-pill metadata-kind-preview-trigger" data-metadata-preview-id="${escapeForAttribute(metadataManagerItemId(item))}" title="Click and hold to preview cover">${escapeHtml(item.kind || '\u2014')}</span>`;
        if (column.key === 'metadataSource') return `<span class="metadata-source-text">${escapeHtml(item.metadataSource || '\u2014')}</span>`;
        if (column.key === 'category' && preferredPlatformReadOnly) return `<input class="metadata-manager-input ${METADATA_MANAGER_WIDE_COLUMNS.has('category') ? 'wide' : ''} readonly" data-id="${escapeForAttribute(id)}" data-field="category" value="${escapeForAttribute(MULTI_PLATFORM_LABEL)}" readonly aria-readonly="true" title="Preferred Platform is read-only when Associated Platforms contains multiple systems." />`;
        return inputField(column.key, metadataManagerFieldValue(item, column.key));
      };
      return `<tr${rowClass}>
        <td><input class="metadata-manager-row-check" type="checkbox" data-id="${escapeForAttribute(id)}" ${selectedSet.has(id) ? 'checked' : ''} /></td>
        ${columns.map(column => `<td>${cellFor(column)}</td>`).join('')}
      </tr>`;
    }).join('') + (hiddenCount ? `<tr><td colspan="${columns.length + 1}" class="metadata-manager-empty metadata-manager-hidden-note">${hiddenCount} more matching entr${hiddenCount === 1 ? 'y is' : 'ies are'} hidden by the current display limit. Use Load More or Show All in the footer to render the rest.</td></tr>` : '') : `<tr><td colspan="${columns.length + 1}" class="metadata-manager-empty">No metadata entries match this filter.</td></tr>`;
  }
  if ($('metadataManagerCount')) {
    const editableTotal = (state.items || []).filter(i => ['Manual','Strategy Guide','Magazine'].includes(i.kind)).length;
    $('metadataManagerCount').textContent = hiddenCount
      ? `${renderedItems.length} shown / ${items.length} matched (${hiddenCount} not rendered yet) / ${editableTotal} editable entries total`
      : `${renderedItems.length} shown / ${items.length} matched / ${editableTotal} editable entries total`;
  }
  const loadMoreButton = $('metadataManagerLoadMoreRows');
  if (loadMoreButton) {
    loadMoreButton.hidden = !hiddenCount;
    loadMoreButton.disabled = !hiddenCount;
    loadMoreButton.textContent = hiddenCount ? `Load ${Math.min(METADATA_MANAGER_RENDER_STEP, hiddenCount)} More` : 'Load More';
  }
  const showAllButton = $('metadataManagerShowAllRows');
  if (showAllButton) {
    showAllButton.hidden = !hiddenCount;
    showAllButton.disabled = !hiddenCount;
    showAllButton.textContent = hiddenCount ? `Show All ${items.length}` : 'Show All';
  }
  const collapseButton = $('metadataManagerCollapseRows');
  if (collapseButton) {
    const canCollapse = renderedItems.length > metadataManagerDefaultRenderLimit();
    collapseButton.hidden = !canCollapse;
    collapseButton.disabled = !canCollapse;
  }
  if ($('metadataManagerDirtyCount')) $('metadataManagerDirtyCount').textContent = `${Object.keys(dirty).length} edited`;
  const headerCheck = $('metadataManagerHeaderCheck');
  if (headerCheck) {
    headerCheck.addEventListener('change', e => metadataManagerSelectRendered(!!e.currentTarget.checked));
    const visibleIds = renderedItems.map(metadataManagerItemId).filter(Boolean);
    headerCheck.checked = visibleIds.length > 0 && visibleIds.every(id => selectedSet.has(id));
  }
}


function metadataManagerHandleHeaderClick(event) {
  const sortButton = event.target.closest?.('[data-column-sort]');
  if (sortButton) {
    event.preventDefault();
    metadataManagerSetSort(sortButton.dataset.columnSort || '');
    return;
  }
  const moveButton = event.target.closest?.('[data-column-move]');
  if (moveButton) {
    event.preventDefault();
    metadataManagerMoveColumn(moveButton.dataset.columnKey || '', Number(moveButton.dataset.columnMove || 0));
  }
}

function metadataManagerHandleHeaderDragStart(event) {
  const header = event.target.closest?.('[data-column-key]');
  if (!header) return;
  state.metadataManager.draggedColumnKey = header.dataset.columnKey || '';
  try { event.dataTransfer.setData('text/plain', state.metadataManager.draggedColumnKey); } catch {}
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
}

function metadataManagerHandleHeaderDragOver(event) {
  if (!state.metadataManager?.draggedColumnKey) return;
  const header = event.target.closest?.('[data-column-key]');
  if (!header) return;
  event.preventDefault();
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
}

function metadataManagerHandleHeaderDrop(event) {
  const target = event.target.closest?.('[data-column-key]');
  const sourceKey = state.metadataManager?.draggedColumnKey || '';
  if (!target || !sourceKey) return;
  event.preventDefault();
  metadataManagerReorderColumn(sourceKey, target.dataset.columnKey || '');
  state.metadataManager.draggedColumnKey = '';
}

function metadataManagerHandleHeaderDragEnd() {
  if (state.metadataManager) state.metadataManager.draggedColumnKey = '';
}


function metadataManagerScheduleRender(delay = METADATA_MANAGER_SEARCH_DEBOUNCE_MS) {
  state.metadataManager = state.metadataManager || {};
  if (state.metadataManager.renderTimer) clearTimeout(state.metadataManager.renderTimer);
  state.metadataManager.renderTimer = setTimeout(() => {
    state.metadataManager.renderTimer = null;
    renderMetadataManager();
  }, Math.max(0, Number(delay) || 0));
}

function metadataManagerUpdateFilter(field, value) {
  state.metadataManager = state.metadataManager || {};
  state.metadataManager[field] = value;
  if (field === 'search') {
    metadataManagerResetRenderLimit();
    metadataManagerSetStatus(value ? 'Searching metadata...' : '', '');
    metadataManagerScheduleRender();
    return;
  }
  if (field === 'filterKind' || field === 'kindFilters' || field === 'statusFilter' || field === 'missing' || field === 'category') {
    state.metadataManager.selectedIds = [];
    metadataManagerResetRenderLimit();
  }
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

function metadataManagerSelectRendered(select = true) {
  const items = metadataManagerEditableItems();
  const renderLimit = metadataManagerCurrentRenderLimit(items.length);
  const renderedIds = items.slice(0, renderLimit).map(metadataManagerItemId).filter(Boolean);
  const selected = new Set(state.metadataManager.selectedIds || []);
  renderedIds.forEach(id => select ? selected.add(id) : selected.delete(id));
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
  const lockFiltered = filterLockedMetadataPayload(item, payload);
  const safePayload = normalizeClientMetadataPayload(lockFiltered.payload);
  metadataManagerUpdateItemLocal(id, safePayload);
  const res = await fetch(`/api/items/${encodeURIComponent(id)}/metadata`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(safePayload)
  });
  if (!res.ok) {
    let message = `HTTP ${res.status}`;
    try { message = (await res.json()).error || message; } catch {}
    throw new Error(message);
  }
  let updated = null;
  try { updated = await res.json(); } catch {}
  if (updated) {
    const merged = mergeSavedMetadataClientSide((state.items || []).find(i => metadataManagerItemId(i) === id) || item, updated, safePayload);
    replaceItemInState(merged);
  }
  return { skippedLockedFields: lockFiltered.skipped || [] };
}

function metadataManagerBuildStateItemIndex() {
  const index = new Map();
  (state.items || []).forEach((item, position) => {
    const id = metadataManagerItemId(item);
    if (id && !index.has(id)) index.set(id, { item, position });
  });
  return index;
}

function metadataManagerYieldToBrowser() {
  return new Promise(resolve => window.setTimeout(resolve, 0));
}

async function metadataManagerApplySavedRowsLocal(rows) {
  const stateIndex = metadataManagerBuildStateItemIndex();
  const selectedId = state.selected ? metadataManagerItemId(state.selected) : '';
  let changed = 0;

  for (let i = 0; i < (rows || []).length; i += 1) {
    const row = rows[i];
    const id = String(row?.id || '').trim();
    if (!id) continue;
    const slot = stateIndex.get(id);
    if (!slot) continue;

    const updated = mergeSavedMetadataClientSide(slot.item, {}, row.payload || {});
    updated.id = updated.id || id;
    updated.Id = updated.Id || id;
    prepareLibraryItemComputedFields(updated);
    state.items[slot.position] = updated;
    slot.item = updated;
    if (selectedId && selectedId === id) state.selected = updated;
    changed += 1;

    if (changed % 250 === 0) await metadataManagerYieldToBrowser();
  }

  if (changed) markLibraryIndexesDirty();
  return changed;
}

async function metadataManagerRefreshServerMetadataCache(ids) {
  const distinctIds = [...new Set((ids || []).map(id => String(id || '').trim()).filter(Boolean))];
  if (!distinctIds.length) return { cacheUpdated: 0 };

  const res = await fetch('/api/items/metadata/refresh-cache', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ids: distinctIds })
  });

  if (!res.ok) {
    let message = `HTTP ${res.status}`;
    try { message = (await res.json()).error || message; } catch {}
    throw new Error(message);
  }

  try { return await res.json(); } catch { return { cacheUpdated: 0 }; }
}

async function metadataManagerPersistBulk(entries) {
  const rows = [];
  let skippedLockedFields = 0;
  const stateIndex = metadataManagerBuildStateItemIndex();

  (entries || []).forEach(entry => {
    const id = String(entry?.id || '').trim();
    if (!id) return;
    const item = entry.item || stateIndex.get(id)?.item || null;
    const lockFiltered = filterLockedMetadataPayload(item, entry.payload || {});
    const safePayload = normalizeClientMetadataPayload(lockFiltered.payload);
    const hasChanges = Object.keys(safePayload || {}).length > 0;
    skippedLockedFields += lockFiltered.skipped?.length || 0;
    if (!hasChanges) return;
    rows.push({ id, payload: safePayload });
  });

  if (!rows.length) return { saved: 0, skippedLockedFields, cacheUpdated: 0, missingIds: [] };

  const chunkSize = 250;
  let saved = 0;
  let cacheUpdated = 0;
  let missingCount = 0;
  const missingIds = [];
  const savedRows = [];

  for (let i = 0; i < rows.length; i += chunkSize) {
    const chunk = rows.slice(i, i + chunkSize);
    if (rows.length > chunkSize) {
      const done = Math.min(i + chunk.length, rows.length);
      metadataManagerSetStatus(`Saving metadata batch ${Math.floor(i / chunkSize) + 1}/${Math.ceil(rows.length / chunkSize)} (${done} of ${rows.length})...`);
    }

    const res = await fetch('/api/items/metadata/bulk', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        updateCache: false,
        updates: chunk.map(row => ({ id: row.id, payload: row.payload }))
      })
    });

    if (!res.ok) {
      let message = `HTTP ${res.status}`;
      try { message = (await res.json()).error || message; } catch {}
      throw new Error(message);
    }

    let data = null;
    try { data = await res.json(); } catch { data = {}; }
    saved += Number(data?.saved || chunk.length) || chunk.length;
    cacheUpdated += Number(data?.cacheUpdated || 0) || 0;
    missingCount += Number(data?.missingCount || 0) || 0;
    if (Array.isArray(data?.missingIds)) missingIds.push(...data.missingIds);
    savedRows.push(...chunk);
    await metadataManagerYieldToBrowser();
  }

  metadataManagerSetStatus(`Applying saved metadata locally (${savedRows.length} row(s))...`);
  const localUpdated = await metadataManagerApplySavedRowsLocal(savedRows);

  let cacheRefreshFailed = '';
  try {
    metadataManagerSetStatus('Refreshing server metadata cache once after bulk save...');
    const refresh = await metadataManagerRefreshServerMetadataCache(savedRows.map(row => row.id));
    cacheUpdated += Number(refresh?.cacheUpdated || 0) || 0;
  } catch (err) {
    cacheRefreshFailed = err?.message || String(err || 'Unknown cache refresh error');
    console.warn('Metadata cache refresh failed after bulk save:', err);
  }

  return {
    saved,
    cacheUpdated,
    localUpdated,
    skippedLockedFields,
    missingCount,
    missingIds,
    cacheRefreshFailed
  };
}

async function metadataManagerSaveDirtyRows() {
  const dirty = { ...(state.metadataManager.dirty || {}) };
  const ids = Object.keys(dirty);
  if (!ids.length) {
    metadataManagerSetStatus('No edited rows to save.', '');
    return;
  }

  metadataManagerSetStatus(`Saving ${ids.length} edited row(s) in bulk batches...`);
  const entries = [];
  const stateIndex = metadataManagerBuildStateItemIndex();
  for (const id of ids) {
    const item = stateIndex.get(id)?.item;
    if (!item) continue;
    entries.push({ id, item, payload: metadataManagerRowPayload(item, dirty[id]) });
  }

  try {
    const result = await metadataManagerPersistBulk(entries);
    state.metadataManager.dirty = {};
    if (ids.length > METADATA_MANAGER_DEFAULT_RENDER_LIMIT) metadataManagerResetRenderLimit();
    const skippedText = result.skippedLockedFields ? ` ${result.skippedLockedFields} locked field value(s) were skipped.` : '';
    const missingText = result.missingIds?.length ? ` ${result.missingIds.length} row(s) were saved but not currently loaded in the cache.` : '';
    const cacheText = result.cacheRefreshFailed ? ` Saved metadata, but cache refresh failed: ${result.cacheRefreshFailed}` : '';
    metadataManagerSetStatus(`Saved ${result.saved} edited row(s) in bulk batches.${skippedText}${missingText}${cacheText}`, result.cacheRefreshFailed ? 'error' : 'success');
  } catch (err) {
    metadataManagerSetStatus(`Bulk save failed: ${err?.message || err}`, 'error');
    throw err;
  } finally {
    renderMetadataManager();
  }
}

async function metadataManagerApplyBatch() {
  const items = metadataManagerSelectedItems();
  if (!items.length) {
    metadataManagerSetStatus('Select one or more rows first.', 'error');
    return;
  }
  metadataManagerRenderBatchEditor();
  const batchEntries = metadataManagerBatchEntries();
  if (!batchEntries.length) {
    metadataManagerSetStatus('Enter at least one visible-column batch value to apply.', 'error');
    return;
  }
  const fieldLabels = batchEntries.map(entry => metadataManagerColumnDefinition(entry.field)?.label || entry.field).join(', ');
  metadataManagerSetStatus(`Applying ${fieldLabels} to ${items.length} selected row(s) in bulk batches...`);
  const entries = items.map(item => ({
    id: metadataManagerItemId(item),
    item,
    payload: metadataManagerBatchPayloadForItem(item, batchEntries)
  }));
  const result = await metadataManagerPersistBulk(entries);
  state.metadataManager.dirty = {};
  if (items.length > METADATA_MANAGER_DEFAULT_RENDER_LIMIT) metadataManagerResetRenderLimit();
  const skippedText = result.skippedLockedFields ? ` ${result.skippedLockedFields} locked field value(s) were skipped.` : '';
  const cacheText = result.cacheRefreshFailed ? ` Cache refresh failed: ${result.cacheRefreshFailed}` : '';
  metadataManagerSetStatus(`Applied batch metadata to ${result.saved} row(s).${skippedText}${cacheText}`, result.cacheRefreshFailed ? 'error' : 'success');
  renderMetadataManager();
}



const METADATA_BATCH_SOURCE_FIELDS = {
  openLibrary: [
    { key: 'title', label: 'Title' },
    { key: 'authorWriter', label: 'Author / Writer' },
    { key: 'publisher', label: 'Guide Publisher' },
    { key: 'publishYear', label: 'Publish Year' },
    { key: 'isbn10', label: 'ISBN-10' },
    { key: 'isbn13', label: 'ISBN-13' },
    { key: 'language', label: 'Language' },
    { key: 'summary', label: 'Description / Summary' },
    { key: 'pageCount', label: 'Page Count' }
  ],
  igdb: [
    { key: 'gameTitle', label: 'Game Title' },
    { key: 'gameDeveloper', label: 'Game Developer' },
    { key: 'gamePublisher', label: 'Game Publisher' },
    { key: 'gameReleaseYear', label: 'Game Release Year' },
    { key: 'gameFranchise', label: 'Game Franchise / Series' },
    { key: 'genre', label: 'Genre' },
    { key: 'associatedPlatforms', label: 'Associated Platforms' },
    { key: 'preferredPlatform', label: 'Preferred Platform' }
  ],
  esrb: [
    { key: 'rating', label: 'ESRB Rating' }
  ]
};

function metadataBatchStrategyItems() {
  return metadataManagerSelectedItems().filter(item => item?.kind === 'Strategy Guide');
}

const METADATA_BATCH_SOURCE_ORDER = ['openLibrary', 'igdb', 'esrb'];

function metadataBatchSourceToggles() {
  return Array.from(document.querySelectorAll('input[data-metadata-batch-source-toggle]'))
    .filter(input => METADATA_BATCH_SOURCE_ORDER.includes(input.dataset.metadataBatchSource || ''));
}

function metadataBatchSelectedSources() {
  const supported = new Set(METADATA_BATCH_SOURCE_ORDER);
  return metadataBatchSourceToggles()
    .filter(input => input.checked && !input.disabled)
    .map(input => input.dataset.metadataBatchSource)
    .filter(source => supported.has(source))
    .slice(0, 1);
}

function metadataBatchActiveSource() {
  return metadataBatchSelectedSources()[0] || '';
}

function metadataBatchSyncSourceLocks(changedInput = null) {
  const toggles = metadataBatchSourceToggles();
  if (!toggles.length) return;
  let activeInput = changedInput?.checked ? changedInput : toggles.find(input => input.checked) || null;
  if (activeInput) {
    toggles.forEach(input => { if (input !== activeInput) input.checked = false; });
  }
  const activeSource = activeInput?.dataset?.metadataBatchSource || '';
  toggles.forEach(input => {
    const source = input.dataset.metadataBatchSource || '';
    const locked = !!activeSource && source !== activeSource;
    input.disabled = locked;
    const pill = input.closest('.metadata-source-provider-pill');
    if (pill) {
      pill.classList.toggle('is-active', !!activeSource && source === activeSource);
      pill.classList.toggle('is-disabled', locked);
      pill.title = locked ? 'Uncheck the active lookup source before selecting this one.' : '';
    }
  });
  document.querySelectorAll('.metadata-source-fields-card').forEach(card => {
    const source = card.dataset.metadataBatchFieldsCard
      || card.querySelector('input[data-metadata-batch-field]')?.dataset?.metadataBatchSource
      || '';
    if (!source) return;
    const locked = !!activeSource && source !== activeSource;
    card.classList.toggle('is-disabled', locked);
    card.setAttribute('aria-disabled', locked ? 'true' : 'false');
    card.querySelectorAll('input[data-metadata-batch-field], button[data-metadata-batch-select-fields], button[data-metadata-batch-clear-fields]').forEach(control => {
      control.disabled = locked;
    });
  });
}

function metadataBatchSelectedFieldMap() {
  const map = { openLibrary: new Set(), igdb: new Set(), esrb: new Set() };
  document.querySelectorAll('input[data-metadata-batch-field]:checked').forEach(input => {
    const source = input.dataset.metadataBatchSource || '';
    const field = input.dataset.metadataBatchField || '';
    if (map[source] && field && !input.disabled) map[source].add(field);
  });
  return map;
}

function metadataBatchMode() {
  return $('metadataBatchSourceImportMode')?.value || 'overwrite';
}

function metadataBatchIsWeakValue(value, key = '') {
  if (value === null || value === undefined) return true;
  if (key === 'pageCount' || key === 'metadataPageCount') return !(Number(value) > 0);
  const text = Array.isArray(value) ? value.join(', ') : String(value || '').trim();
  if (!text) return true;
  const lower = text.toLowerCase();
  return lower === 'unknown'
    || lower === 'unsorted'
    || lower === 'unsorted strategy guides'
    || lower === 'strategy guide'
    || lower === 'n/a'
    || lower === 'na'
    || lower === 'none'
    || lower === '0'
    || lower === '-';
}

function metadataBatchCurrentFieldValue(item, source, field) {
  if (!item) return '';
  if (source === 'openLibrary') {
    if (field === 'title') return item.title || item.strategyGuideTitle || '';
    if (field === 'authorWriter') return item.writer || '';
    if (field === 'publishYear') return item.year || '';
    if (field === 'language') return item.languageTag || item.language || '';
    if (field === 'pageCount') return item.pageCount || item.metadataPageCount || '';
    if (field === 'summary') return item.summary || '';
    return item[field] || '';
  }
  if (source === 'igdb') {
    if (field === 'gameDeveloper') return item.gameDeveloper || item.developer || '';
    if (field === 'gamePublisher') return item.gamePublisher || '';
    if (field === 'gameFranchise') return item.gameFranchise || item.franchise || item.series || '';
    if (field === 'associatedPlatforms') return associatedPlatformsOf(item).join(', ');
    if (field === 'preferredPlatform') return metadataManagerCategoryValue(item) || '';
    return item[field] || '';
  }
  if (source === 'esrb') return item.rating || '';
  return '';
}

function metadataBatchShouldApplyField(item, source, field, incomingValue, mode) {
  if (metadataBatchIsWeakValue(incomingValue, field)) return false;
  if (mode === 'overwrite') return true;
  const current = metadataBatchCurrentFieldValue(item, source, field);
  if (mode === 'empty') return metadataBatchIsWeakValue(current, field);
  return metadataBatchIsWeakValue(current, field);
}

function metadataBatchItemSearchText(item, keys) {
  return keys.map(key => String(item?.[key] || '').trim()).find(Boolean) || '';
}

function metadataBatchCleanLookupText(value = '') {
  return String(value || '')
    .replace(/\([^)]*\)/g, ' ')
    .replace(/\b(official|unofficial|unauthorized|complete|ultimate|prima|bradygames|brady|versus|versus books|strategy guide|guide|walkthrough|secrets?|tips?|codes?|hint book|player'?s guide)\b/gi, ' ')
    .replace(/[#:\-_/\\]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function metadataBatchNormalizeLookupText(value = '') {
  return String(value || '').toLowerCase().replace(/[^a-z0-9]+/g, ' ').replace(/\s+/g, ' ').trim();
}

function metadataBatchLookupTokens(value = '') {
  const stop = new Set(['the','a','an','and','or','of','for','to','in','on','with','official','unofficial','guide','strategy','complete','unauthorized','secrets','tips','codes','prima','bradygames','brady']);
  return metadataBatchNormalizeLookupText(value).split(' ').filter(token => token.length > 2 && !stop.has(token));
}

function metadataBatchTokenCoverage(candidate = '', expected = '') {
  const tokens = metadataBatchLookupTokens(expected);
  if (!tokens.length) return 0;
  const haystack = ` ${metadataBatchNormalizeLookupText(candidate)} `;
  const matched = tokens.filter(token => haystack.includes(` ${token} `)).length;
  return matched / tokens.length;
}

function metadataBatchYearDistance(a, b) {
  const ay = Number(String(a || '').match(/\d{4}/)?.[0] || 0);
  const by = Number(String(b || '').match(/\d{4}/)?.[0] || 0);
  if (!ay || !by) return 999;
  return Math.abs(ay - by);
}

function metadataBatchOpenLibrarySearchPlan(item) {
  const title = metadataBatchItemSearchText(item, ['strategyGuideTitle', 'title', 'name']) || metadataManagerItemName(item);
  const gameTitle = metadataBatchItemSearchText(item, ['gameTitle', 'platformMatchTitle', 'franchise', 'series']);
  const publisher = String(item?.publisher || '').trim();
  const year = String(item?.year || item?.publishYear || '').trim();
  const isbn = String(item?.isbn10 || item?.isbn13 || item?.isbn || '').trim();
  const cleanedTitle = metadataBatchCleanLookupText(title);
  const secondaryParts = [];
  if (gameTitle && !openLibrarySameText(gameTitle, title)) secondaryParts.push(gameTitle);
  if (publisher) secondaryParts.push(publisher);
  if (year) secondaryParts.push(year);
  const queryCandidates = [
    isbn,
    title,
    cleanedTitle && cleanedTitle !== title ? cleanedTitle : '',
    gameTitle && !openLibrarySameText(gameTitle, title) ? `${gameTitle} strategy guide` : '',
    gameTitle && !openLibrarySameText(gameTitle, title) ? gameTitle : '',
    [cleanedTitle || title, publisher].filter(Boolean).join(' '),
    [gameTitle, publisher].filter(Boolean).join(' ')
  ].map(v => String(v || '').trim()).filter(Boolean);
  const seen = new Set();
  const queries = queryCandidates.filter(value => {
    const key = metadataBatchNormalizeLookupText(value);
    if (!key || seen.has(key)) return false;
    seen.add(key);
    return true;
  });
  return { q: queries[0] || title || gameTitle || isbn, queries, secondary: secondaryParts.join(' '), isbn, title, cleanedTitle, gameTitle, publisher, year };
}

function metadataBatchScoreOpenLibraryResult(result, item, plan, query = '') {
  const candidateTitle = String(result?.title || '').trim();
  const candidateText = [candidateTitle, result?.authorWriter, result?.publisher, result?.publishYear, result?.description].join(' ');
  let score = 0;
  const titleCoverage = Math.max(metadataBatchTokenCoverage(candidateTitle, plan.title), metadataBatchTokenCoverage(candidateText, plan.title));
  const cleanCoverage = Math.max(metadataBatchTokenCoverage(candidateTitle, plan.cleanedTitle), metadataBatchTokenCoverage(candidateText, plan.cleanedTitle));
  const gameCoverage = Math.max(metadataBatchTokenCoverage(candidateTitle, plan.gameTitle), metadataBatchTokenCoverage(candidateText, plan.gameTitle));
  score += Math.round(Math.max(titleCoverage, cleanCoverage) * 55);
  score += Math.round(gameCoverage * 25);
  if (plan.isbn) {
    const wanted = plan.isbn.replace(/[^0-9x]/gi, '').toLowerCase();
    const have = [result?.isbn10, result?.isbn13, result?.isbn].flat().map(v => String(v || '').replace(/[^0-9x]/gi, '').toLowerCase());
    if (wanted && have.includes(wanted)) score += 80;
  }
  if (plan.publisher && metadataBatchTokenCoverage(result?.publisher || '', plan.publisher) > 0) score += 15;
  const distance = metadataBatchYearDistance(result?.publishYear || result?.year, plan.year);
  if (distance === 0) score += 15; else if (distance <= 2) score += 8; else if (distance <= 5) score += 3;
  if (result?.coverPreviewUrl) score += 4;
  const confidence = String(result?.confidence || '').toLowerCase();
  if (confidence.includes('high')) score += 10;
  if (confidence.includes('medium')) score += 4;
  if (metadataBatchNormalizeLookupText(candidateTitle) === metadataBatchNormalizeLookupText(plan.title)) score += 35;
  if (metadataBatchNormalizeLookupText(candidateTitle) === metadataBatchNormalizeLookupText(plan.cleanedTitle)) score += 25;
  if (query && metadataBatchTokenCoverage(candidateTitle, query) > 0.7) score += 8;
  return score;
}

async function metadataBatchSearchOpenLibraryCandidates(plan, item, signal = null) {
  const candidates = [];
  const seen = new Set();
  const queries = plan.queries?.length ? plan.queries : [plan.q];
  for (const query of queries.slice(0, 6)) {
    const params = new URLSearchParams({ q: query, limit: '12' });
    ['secondary','isbn','title','gameTitle','publisher','year'].forEach(key => { if (plan[key]) params.set(key, plan[key]); });
    metadataBatchEnsureNotCanceled(signal);
    const data = await metadataBatchFetchJson(`/api/openlibrary/search?${params.toString()}`, { signal });
    (Array.isArray(data?.results) ? data.results : []).slice(0, 8).forEach(result => {
      const key = [result.workKey, result.editionKey, result.key, result.title, result.isbn10, result.isbn13].flat().filter(Boolean).join('|').toLowerCase();
      if (!key || seen.has(key)) return;
      seen.add(key);
      candidates.push({ result, query, score: metadataBatchScoreOpenLibraryResult(result, item, plan, query) });
    });
    if (candidates.some(candidate => candidate.score >= 85)) break;
    await metadataBatchDelay(80, signal);
  }
  return candidates.sort((a, b) => b.score - a.score);
}

async function metadataBatchResolveOpenLibrary(item, signal = null) {
  metadataBatchEnsureNotCanceled(signal);
  const plan = metadataBatchOpenLibrarySearchPlan(item);
  if (!plan.q) return { status: 'skipped', message: 'Missing guide title/game title.' };
  const candidates = await metadataBatchSearchOpenLibraryCandidates(plan, item, signal);
  const best = candidates[0]?.result || null;
  if (!best) return { status: 'no-match', message: 'No Open Library match.' };
  metadataBatchEnsureNotCanceled(signal);
  const resolved = await metadataBatchFetchJson('/api/openlibrary/resolve', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(best),
    signal
  });
  const score = candidates[0]?.score || 0;
  const confidence = score >= 85 ? 'High' : score >= 55 ? 'Medium' : 'Review';
  return { status: 'found', result: resolved, message: openLibraryResultTitle(resolved), confidence: resolved.confidence || best.confidence || confidence, matchBy: `Batch best match (${confidence}, score ${score})` };
}

function metadataBatchIgdbSearchPlan(item) {
  const gameTitle = metadataBatchItemSearchText(item, ['gameTitle', 'platformMatchTitle', 'series', 'franchise']) || metadataManagerItemName(item);
  return {
    q: gameTitle,
    platform: metadataManagerCategoryValue(item) || associatedPlatformsOf(item)[0] || '',
    year: item?.gameReleaseYear || item?.year || ''
  };
}

function metadataBatchEsrbSearchPlan(item) {
  return {
    q: metadataBatchItemSearchText(item, ['gameTitle', 'platformMatchTitle', 'series', 'franchise']) || metadataManagerItemName(item),
    platform: metadataManagerCategoryValue(item) || associatedPlatformsOf(item)[0] || ''
  };
}

function metadataBatchFriendlySourceError(source, message = '') {
  const text = String(message || '').replace(/\s+/g, ' ').trim();
  if (source === 'igdb') {
    if (/credentials are not configured/i.test(text)) return 'IGDB credentials are not configured. Add your Twitch Client ID and Client Secret in Settings > Server > Integrations > IGDB.';
    if (/twitch rejected|oauth|client id|client secret|credential|403|401|400/i.test(text)) return 'IGDB credentials were rejected by Twitch. Re-save the Twitch Developer Client ID and generated Client Secret, then use Test IGDB Credentials.';
    if (/failed to fetch|network|timeout/i.test(text)) return 'IGDB lookup could not reach Twitch/IGDB. Check the server network connection and try again.';
  }
  return text || 'Unknown error';
}


function metadataBatchAbortError() {
  try { return new DOMException('Batch lookup was canceled.', 'AbortError'); }
  catch { const err = new Error('Batch lookup was canceled.'); err.name = 'AbortError'; return err; }
}

function metadataBatchIsAbortError(err) {
  return err?.name === 'AbortError' || /aborted|canceled|cancelled/i.test(String(err?.message || err || ''));
}

function metadataBatchEnsureNotCanceled(signal) {
  if (signal?.aborted) throw metadataBatchAbortError();
}

function metadataBatchDelay(ms, signal = null) {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) { reject(metadataBatchAbortError()); return; }
    const timer = setTimeout(resolve, ms);
    if (signal) {
      signal.addEventListener('abort', () => {
        clearTimeout(timer);
        reject(metadataBatchAbortError());
      }, { once: true });
    }
  });
}

function metadataBatchNextRunId() {
  const current = Number(state.metadataSourceBatch?.runId || 0);
  return current + 1;
}

function metadataBatchIsCurrentRun(runId, signal = null) {
  return Number(state.metadataSourceBatch?.runId || 0) === Number(runId || 0) && !signal?.aborted;
}

function metadataBatchCancelActiveRun() {
  const batch = state.metadataSourceBatch;
  if (batch?.running && batch.abortController) {
    try { batch.abortController.abort(); } catch {}
    batch.running = false;
  }
}

async function metadataBatchPreflightSelectedSources(sources = [], signal = null) {
  const blocked = {};
  if (sources.includes('igdb')) {
    try {
      metadataBatchEnsureNotCanceled(signal);
      await metadataBatchFetchJson('/api/igdb/status', { signal });
    } catch (err) {
      blocked.igdb = metadataBatchFriendlySourceError('igdb', err?.message || err);
    }
  }
  return blocked;
}

function metadataBatchBlockedSourcesHtml(blockedSources = {}) {
  const entries = Object.entries(blockedSources || {}).filter(([, message]) => message);
  if (!entries.length) return '';
  const labels = { openLibrary: 'Open Library', igdb: 'IGDB', esrb: 'ESRB' };
  return `<div class="metadata-batch-source-warning">${entries.map(([source, message]) => `<strong>${escapeHtml(labels[source] || source)} skipped:</strong> ${escapeHtml(message)}`).join('<br />')}</div>`;
}

async function metadataBatchFetchJson(url, options = {}) {
  metadataBatchEnsureNotCanceled(options?.signal);
  const res = await fetch(url, { cache: 'no-store', ...options });
  let data = null;
  try { data = await res.json(); } catch {}
  if (!res.ok) throw new Error(data?.error || `HTTP ${res.status}`);
  return data;
}

async function metadataBatchResolveIgdb(item, signal = null) {
  metadataBatchEnsureNotCanceled(signal);
  const plan = metadataBatchIgdbSearchPlan(item);
  if (!plan.q) return { status: 'skipped', message: 'Missing game title.' };
  const params = new URLSearchParams({ q: plan.q, limit: '6' });
  if (plan.platform) params.set('platform', plan.platform);
  if (plan.year) params.set('year', plan.year);
  const data = await metadataBatchFetchJson(`/api/igdb/search?${params.toString()}`, { signal });
  const result = Array.isArray(data?.results) ? data.results[0] : null;
  if (!result) return { status: 'no-match', message: 'No IGDB match.' };
  metadataBatchEnsureNotCanceled(signal);
  const resolved = await metadataBatchFetchJson('/api/igdb/resolve', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(result),
    signal
  });
  return { status: 'found', result: resolved, message: igdbResultTitle(resolved), confidence: resolved.confidence || result.confidence || '' };
}

async function metadataBatchResolveEsrb(item, signal = null) {
  metadataBatchEnsureNotCanceled(signal);
  const plan = metadataBatchEsrbSearchPlan(item);
  if (!plan.q) return { status: 'skipped', message: 'Missing game title.' };
  const params = new URLSearchParams({ q: plan.q, limit: '6' });
  if (plan.platform) params.set('platform', plan.platform);
  const data = await metadataBatchFetchJson(`/api/esrb/search?${params.toString()}`, { signal });
  const result = Array.isArray(data?.results) ? data.results[0] : null;
  if (!result) return { status: 'no-match', message: 'No ESRB match.' };
  metadataBatchEnsureNotCanceled(signal);
  const resolved = await metadataBatchFetchJson('/api/esrb/resolve', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(result),
    signal
  });
  return { status: 'found', result: resolved, message: esrbResultTitle(resolved), confidence: resolved.confidence || result.confidence || '' };
}

function metadataBatchOpenLibraryFieldValue(result, field) {
  return openLibraryCleanValue(field, result?.[field]);
}

function metadataBatchIgdbFieldValue(result, field) {
  if (field === 'associatedPlatforms') return normalizeGuidevaultPlatformList(result?.associatedPlatforms).join(', ');
  if (field === 'gameDeveloper') return igdbListLabel(result?.developers);
  if (field === 'gamePublisher') return igdbListLabel(result?.publishers);
  if (field === 'genre') return igdbListLabel(result?.genres);
  if (field === 'preferredPlatform') return normalizeGuidevaultPlatformName(result?.preferredPlatform);
  if (field === 'gameTitle') return result?.gameTitle || result?.name || '';
  return String(result?.[field] || '').trim();
}

function metadataBatchEsrbFieldValue(result, field) {
  return field === 'rating' ? String(result?.ratingShort || result?.rating || '').trim() : '';
}


function metadataBatchLockKeyForSourceField(source, field) {
  if (source === 'openLibrary') {
    return ({ title: 'title', authorWriter: 'writer', publisher: 'publisher', publishYear: 'year', isbn10: 'isbn10', isbn13: 'isbn13', language: 'languageTag', summary: 'summary', pageCount: 'pageCount' })[field] || field;
  }
  if (source === 'igdb') {
    return ({ gameTitle: 'gameTitle', gameDeveloper: 'developer', gamePublisher: 'gamePublisher', gameReleaseYear: 'gameReleaseYear', gameFranchise: 'franchise', associatedPlatforms: 'associatedPlatforms', preferredPlatform: 'preferredPlatform' })[field] || field;
  }
  if (source === 'esrb') return 'rating';
  return field;
}

function metadataBatchApplyFieldToPayload(payload, source, field, value, result) {
  if (source === 'openLibrary') {
    if (field === 'pageCount') { payload.pageCount = Number(value); payload.metadataPageCount = Number(value); return; }
    if (field === 'language') { payload.languageTag = normalizeOpenLibraryLanguage(value); payload.language = normalizeOpenLibraryLanguage(value); return; }
    if (field === 'publishYear') { payload.year = value; return; }
    if (field === 'authorWriter') { payload.writer = value; return; }
    if (field === 'title') { payload.title = value; return; }
    payload[field] = value;
    return;
  }
  if (source === 'igdb') {
    if (field === 'associatedPlatforms') { const platforms = itemArray(value); payload.associatedPlatforms = platforms; payload.coveredPlatforms = platforms; return; }
    if (field === 'preferredPlatform') { payload.category = value; payload.system = value; payload.preferredPlatform = value; return; }
    if (field === 'gameTitle') { payload.gameTitle = value; payload.platformMatchTitle = value; return; }
    if (field === 'gameDeveloper') { payload.developer = value; payload.gameDeveloper = value; return; }
    if (field === 'gamePublisher') { payload.gamePublisher = value; return; }
    if (field === 'gameFranchise') { payload.franchise = value; payload.gameFranchise = value; return; }
    payload[field] = value;
    return;
  }
  if (source === 'esrb' && field === 'rating') {
    payload.rating = value;
    if (result?.sourceUrl) payload.esrbUrl = result.sourceUrl;
    if (result?.id) payload.esrbId = String(result.id);
  }
}

function metadataBatchBuildPayload(item, batchResult, fieldMap, mode) {
  const payload = {};
  const sourcesUsed = [];
  const changedLabels = [];
  const activeSources = new Set(metadataBatchSelectedSources());
  const addSource = source => { if (!sourcesUsed.includes(source)) sourcesUsed.push(source); };
  const sourceLabels = { openLibrary: 'Open Library', igdb: 'IGDB', esrb: 'ESRB' };
  Object.entries(batchResult.sources || {}).forEach(([source, sourceResult]) => {
    if (!activeSources.has(source)) return;
    if (sourceResult?.status !== 'found' || !sourceResult.result) return;
    const selectedFields = Array.from(fieldMap[source] || []);
    selectedFields.forEach(field => {
      const value = source === 'openLibrary'
        ? metadataBatchOpenLibraryFieldValue(sourceResult.result, field)
        : source === 'igdb'
          ? metadataBatchIgdbFieldValue(sourceResult.result, field)
          : metadataBatchEsrbFieldValue(sourceResult.result, field);
      if (!metadataBatchShouldApplyField(item, source, field, value, mode)) return;
      if (isMetadataFieldLocked(item, metadataBatchLockKeyForSourceField(source, field))) return;
      metadataBatchApplyFieldToPayload(payload, source, field, value, sourceResult.result);
      const definition = (METADATA_BATCH_SOURCE_FIELDS[source] || []).find(f => f.key === field);
      changedLabels.push(definition?.label || field);
      addSource(sourceLabels[source] || source);
    });
    if (source === 'igdb' && selectedFields.length && sourceResult.result?.id) {
      payload.igdbId = String(sourceResult.result.id);
      payload.igdbUrl = sourceResult.result.sourceUrl || '';
      addSource(sourceLabels[source] || source);
    }
  });
  if (Object.keys(payload).length) {
    payload.metadataSource = `Batch lookup: ${sourcesUsed.length ? sourcesUsed.join(', ') : metadataBatchSourceLabels()}`;
    payload.metadataStatus = 'Needs Review';
  }
  if (item?.kind === 'Strategy Guide') {
    const platformSource = payload.associatedPlatforms !== undefined ? itemArray(payload.associatedPlatforms) : associatedPlatformsOf(item);
    if (hasMultipleAssociatedPlatforms(platformSource)) {
      payload.category = MULTI_PLATFORM_LABEL;
      payload.system = MULTI_PLATFORM_LABEL;
      payload.preferredPlatform = MULTI_PLATFORM_LABEL;
    }
  }
  return { payload, changedLabels };
}


function metadataBatchLookupSourceForRow(row) {
  const sources = row?.sources || {};
  const active = row?.activeSource || state.metadataSourceBatch?.activeSource || metadataBatchActiveSource();
  if (METADATA_BATCH_SOURCE_ORDER.includes(active) && sources[active]) return active;
  return METADATA_BATCH_SOURCE_ORDER.find(source => sources[source]) || active || 'openLibrary';
}

function metadataBatchLookupPreviewForSource(source, entry) {
  const labels = { openLibrary: 'OL', igdb: 'IGDB', esrb: 'ESRB' };
  if (source === 'esrb') {
    const rating = entry?.result?.ratingShort || entry?.result?.rating || '';
    return {
      label: labels[source],
      imageUrl: rating ? esrbIconUrl(rating) : '',
      alt: rating ? esrbDisplayLabel(rating) : 'No ESRB rating',
      emptyText: 'No rating',
      kind: 'esrb'
    };
  }
  return {
    label: labels[source] || 'Lookup',
    imageUrl: entry?.result?.coverPreviewUrl || '',
    alt: source === 'igdb' ? 'IGDB cover preview' : 'Open Library cover preview',
    emptyText: 'No cover',
    kind: 'cover'
  };
}

function metadataBatchCoverCompareHtml(item, row) {
  const title = metadataManagerItemName(item) || item?.title || 'Strategy Guide';
  const currentCover = coverUrl(item) || '/assets/missing-cover.svg';
  const source = metadataBatchLookupSourceForRow(row);
  const entry = row?.sources?.[source] || null;
  const preview = metadataBatchLookupPreviewForSource(source, entry);
  const previewClass = preview.kind === 'esrb' ? ' is-esrb' : '';
  const previewImg = preview.imageUrl
    ? `<img class="metadata-batch-lookup-preview-img${previewClass}" src="${escapeForAttribute(preview.imageUrl)}" alt="${escapeForAttribute(preview.alt)}" loading="lazy" onerror="this.onerror=null;this.src='/assets/${preview.kind === 'esrb' ? 'ESRB/RatedNone.png' : 'missing-cover.svg'}';" />`
    : `<span class="metadata-batch-lookup-preview-empty${previewClass}">${escapeHtml(preview.emptyText)}</span>`;
  return `<div class="metadata-batch-cover-compare">
    <figure><img class="metadata-batch-gv-cover" src="${escapeForAttribute(currentCover)}" alt="${escapeForAttribute(title)} cover" loading="lazy" onerror="this.onerror=null;this.src='/assets/missing-cover.svg';" /><figcaption>GV</figcaption></figure>
    <figure>${previewImg}<figcaption>${escapeHtml(preview.label)}</figcaption></figure>
  </div>`;
}

function metadataBatchStatusClass(row) {
  const applyStatus = String(row?.applyStatus || '').toLowerCase();
  if (applyStatus.includes('applied')) return 'success';
  if (applyStatus.includes('failed') || applyStatus.includes('error')) return 'error';
  if (applyStatus.includes('skipped')) return 'muted';
  const statuses = Object.values(row?.sources || {}).map(source => String(source?.status || '').toLowerCase()).filter(Boolean);
  if (!statuses.length) return '';
  if (statuses.some(status => status === 'found')) return 'success';
  if (statuses.some(status => status === 'error' || status.includes('failed'))) return 'error';
  if (statuses.every(status => status === 'no-match' || status === 'skipped')) return 'muted';
  return '';
}

function metadataBatchSourceStatusCounts(row) {
  const counts = { found: 0, error: 0, skipped: 0, noMatch: 0, total: 0 };
  Object.values(row?.sources || {}).forEach(source => {
    const status = String(source?.status || '').toLowerCase();
    if (!status) return;
    counts.total += 1;
    if (status === 'found') counts.found += 1;
    else if (status === 'error' || status.includes('failed')) counts.error += 1;
    else if (status === 'skipped') counts.skipped += 1;
    else counts.noMatch += 1;
  });
  return counts;
}

function metadataBatchRowStatus(row, built) {
  if (row?.applyStatus) return row.applyStatus;
  const counts = metadataBatchSourceStatusCounts(row);
  if (!counts.total) return 'Not run';
  if (built?.changedLabels?.length) return 'Ready to apply';
  if (counts.found) return 'Found - no selected field changes';
  if (counts.error && counts.error === counts.total) return 'Lookup failed';
  if (counts.error) return 'Partial lookup error';
  return 'No match';
}

function metadataBatchResultSummary(sourceKey, entry) {
  if (!entry) return 'Not run';
  const label = sourceKey === 'openLibrary' ? 'Open Library' : sourceKey === 'igdb' ? 'IGDB' : 'ESRB';
  if (entry.status === 'found') return `${label}: ${entry.message || 'Found'}${entry.confidence ? ` (${entry.confidence})` : ''}`;
  if (entry.status === 'error') return `${label}: Error - ${entry.message || 'Unknown error'}`;
  return `${label}: ${entry.message || entry.status || 'No match'}`;
}

function metadataBatchResultDetail(sourceKey, entry) {
  const result = entry?.result || {};
  if (!result) return '';
  if (sourceKey === 'openLibrary') {
    return [result.authorWriter, result.publisher, result.publishYear].filter(Boolean).join(' - ');
  }
  if (sourceKey === 'igdb') {
    return [igdbListLabel(result.developers), igdbListLabel(result.publishers), result.gameReleaseYear, igdbListLabel(result.associatedPlatforms)].filter(Boolean).join(' - ');
  }
  if (sourceKey === 'esrb') {
    return [result.ratingShort || result.rating, result.platform].filter(Boolean).join(' - ');
  }
  return '';
}

function metadataBatchSourceSummaryHtml(row) {
  const order = ['openLibrary', 'igdb', 'esrb'];
  return order
    .filter(source => row?.sources?.[source])
    .map(source => {
      const entry = row.sources[source];
      const status = String(entry?.status || '').toLowerCase();
      const detail = metadataBatchResultDetail(source, entry);
      const statusClass = status === 'found' ? 'is-found' : status === 'error' ? 'is-error' : 'is-muted';
      return `<div class="metadata-batch-source-summary ${statusClass}"><strong>${escapeHtml(metadataBatchResultSummary(source, entry))}</strong>${detail ? `<small>${escapeHtml(detail)}</small>` : ''}</div>`;
    }).join('');
}

function metadataBatchGuideCellHtml(item, row) {
  const title = row?.title || metadataManagerItemName(item) || 'Strategy Guide';
  const platform = metadataManagerCategoryValue(item) || item?.primarySystem || item?.system || item?.category || '';
  return `<div class="metadata-batch-guide-cell"><strong>${escapeHtml(title)}</strong>${platform ? `<small>${escapeHtml(platform)}</small>` : ''}</div>`;
}

function metadataBatchSourceLabels(sources = metadataBatchSelectedSources()) {
  const labels = { openLibrary: 'Open Library', igdb: 'IGDB', esrb: 'ESRB' };
  return sources.map(source => labels[source] || source).join(', ');
}

function renderMetadataBatchSourceResults() {
  const host = $('metadataBatchSourceResults');
  if (!host) return;
  const results = state.metadataSourceBatch?.results || [];
  const blockedSources = state.metadataSourceBatch?.blockedSources || {};
  const blockedHtml = metadataBatchBlockedSourcesHtml(blockedSources);
  if (!results.length) {
    const running = !!state.metadataSourceBatch?.running;
    const emptyMessage = running
      ? `Batch lookup is running for ${escapeHtml(metadataBatchSourceLabels([state.metadataSourceBatch?.activeSource || metadataBatchActiveSource()].filter(Boolean)) || 'selected source')}...`
      : 'No batch lookup results yet. Select Strategy Guide rows, choose lookup sources, then run the lookup.';
    host.innerHTML = `${blockedHtml}<p class="sub">${emptyMessage}</p>`;
    return;
  }
  host.innerHTML = `
    ${blockedHtml}
    <div class="metadata-batch-source-results-head">
      <strong>${results.length} result row(s)</strong>
      <span>Review matches and covers where available, uncheck bad matches, then apply checked rows.</span>
    </div>
    <div class="metadata-manager-table-wrap metadata-batch-source-table-wrap">
      <table class="metadata-manager-table metadata-batch-source-table metadata-batch-source-table-compact">
        <thead><tr><th>Apply</th><th>Covers</th><th>Strategy Guide</th><th>Lookup Matches</th><th>Fields Ready</th><th>Status</th></tr></thead>
        <tbody>${results.map(row => {
          const item = row.item || {};
          const idRaw = row.id || '';
          const id = escapeForAttribute(idRaw);
          const fieldMap = metadataBatchSelectedFieldMap();
          const built = metadataBatchBuildPayload(item, row, fieldMap, metadataBatchMode());
          const hasFound = Object.values(row.sources || {}).some(source => source?.status === 'found');
          const statusClass = metadataBatchStatusClass(row);
          return `<tr data-batch-id="${id}" class="${row.error ? 'metadata-batch-row-error' : ''} ${statusClass ? `metadata-batch-status-${statusClass}` : ''}">
            <td><input type="checkbox" class="metadata-batch-row-check" data-batch-id="${id}" ${row.checked === false ? '' : 'checked'} ${hasFound ? '' : 'disabled'} /></td>
            <td>${metadataBatchCoverCompareHtml(item, row)}</td>
            <td>${metadataBatchGuideCellHtml(item, row)}</td>
            <td class="metadata-batch-match-cell">${metadataBatchSourceSummaryHtml(row) || '<small>No lookups selected</small>'}</td>
            <td class="metadata-batch-fields-cell">${escapeHtml(built.changedLabels.length ? built.changedLabels.join(', ') : 'No selected changes')}</td>
            <td><span class="metadata-batch-row-status">${escapeHtml(metadataBatchRowStatus(row, built))}</span></td>
          </tr>`;
        }).join('')}</tbody>
      </table>
    </div>`;
}

async function metadataManagerRunBatchSourceLookup() {
  const items = metadataBatchStrategyItems();
  if (!items.length) { metadataManagerSetStatus('Select one or more Strategy Guide rows first.', 'error'); return; }
  const selectedSources = metadataBatchSelectedSources();
  if (!selectedSources.length) { metadataManagerSetStatus('Select one lookup source: Open Library, IGDB, or ESRB.', 'error'); return; }
  const previousWasRunning = !!state.metadataSourceBatch?.running;
  metadataBatchCancelActiveRun();
  const runId = metadataBatchNextRunId();
  const abortController = new AbortController();
  const signal = abortController.signal;
  const resolvers = { openLibrary: metadataBatchResolveOpenLibrary, igdb: metadataBatchResolveIgdb, esrb: metadataBatchResolveEsrb };
  state.metadataSourceBatch = { results: [], running: true, applied: 0, blockedSources: {}, activeSource: selectedSources[0] || '', runId, abortController };
  renderMetadataBatchSourceResults();
  metadataManagerSetStatus(`${previousWasRunning ? 'Canceled the previous batch lookup. ' : ''}Checking ${metadataBatchSourceLabels(selectedSources)} batch lookup source...`, '');
  let activeSources = [];
  try {
    const blockedSources = await metadataBatchPreflightSelectedSources(selectedSources, signal);
    if (!metadataBatchIsCurrentRun(runId, signal)) return;
    activeSources = selectedSources.filter(source => !blockedSources[source]);
    state.metadataSourceBatch.blockedSources = blockedSources;
    if (!activeSources.length) {
      state.metadataSourceBatch.running = false;
      state.metadataSourceBatch.abortController = null;
      metadataManagerSetStatus(`Batch lookup stopped. ${Object.values(blockedSources).join(' ')}`, 'error');
      renderMetadataBatchSourceResults();
      return;
    }
    renderMetadataBatchSourceResults();
    const blockedSourceEntries = Object.fromEntries(Object.entries(blockedSources).map(([source, message]) => [source, { status: 'error', message }]));
    const skippedLabel = Object.keys(blockedSources).length ? ` Skipped ${metadataBatchSourceLabels(Object.keys(blockedSources))}.` : '';
    metadataManagerSetStatus(`Running ${metadataBatchSourceLabels(activeSources)} batch lookup for ${items.length} Strategy Guide row(s)...${skippedLabel}`, '');
    let completed = 0;
    for (const item of items) {
      metadataBatchEnsureNotCanceled(signal);
      if (!metadataBatchIsCurrentRun(runId, signal)) return;
      const row = { id: metadataManagerItemId(item), title: metadataManagerItemName(item), item, checked: true, sources: { ...blockedSourceEntries }, applyStatus: '', activeSource: activeSources[0] || state.metadataSourceBatch?.activeSource || '', runId };
      for (const source of activeSources) {
        metadataBatchEnsureNotCanceled(signal);
        if (!metadataBatchIsCurrentRun(runId, signal)) return;
        const resolver = resolvers[source];
        if (!resolver) continue;
        try {
          row.sources[source] = await resolver(item, signal);
        } catch (err) {
          if (metadataBatchIsAbortError(err) || !metadataBatchIsCurrentRun(runId, signal)) return;
          row.sources[source] = { status: 'error', message: metadataBatchFriendlySourceError(source, err?.message || String(err)) };
        }
        await metadataBatchDelay(90, signal);
      }
      if (!metadataBatchIsCurrentRun(runId, signal)) return;
      if (!Object.values(row.sources).some(source => source?.status === 'found')) row.checked = false;
      state.metadataSourceBatch.results.push(row);
      completed += 1;
      metadataManagerSetStatus(`Batch lookup ${completed}/${items.length} complete...`, '');
      renderMetadataBatchSourceResults();
      await metadataBatchDelay(180, signal);
    }
    if (!metadataBatchIsCurrentRun(runId, signal)) return;
    state.metadataSourceBatch.running = false;
    state.metadataSourceBatch.abortController = null;
    const skippedDoneLabel = Object.keys(state.metadataSourceBatch.blockedSources || {}).length ? ` Skipped ${metadataBatchSourceLabels(Object.keys(state.metadataSourceBatch.blockedSources))}.` : '';
    metadataManagerSetStatus(`Batch lookup complete.${skippedDoneLabel} Review matches, then apply checked rows.`, 'success');
    renderMetadataBatchSourceResults();
  } catch (err) {
    if (metadataBatchIsAbortError(err) || !metadataBatchIsCurrentRun(runId, signal)) return;
    state.metadataSourceBatch.running = false;
    state.metadataSourceBatch.abortController = null;
    metadataManagerSetStatus(`Batch source lookup failed: ${err?.message || err}`, 'error');
    renderMetadataBatchSourceResults();
  }
}

async function metadataManagerApplyBatchSourceResults() {
  if (state.metadataSourceBatch?.running) { metadataManagerSetStatus('Wait for the active batch lookup to finish before applying results.', 'error'); return; }
  const results = state.metadataSourceBatch?.results || [];
  if (!results.length) { metadataManagerSetStatus('Run a batch lookup first.', 'error'); return; }
  const fieldMap = metadataBatchSelectedFieldMap();
  const mode = metadataBatchMode();
  let skipped = 0;
  let failed = 0;
  const entries = [];
  metadataManagerSetStatus('Applying checked batch lookup results in bulk batches...', '');

  for (const row of results) {
    const checked = document.querySelector(`.metadata-batch-row-check[data-batch-id="${CSS.escape(row.id || '')}"]`)?.checked ?? row.checked !== false;
    if (!checked) { row.applyStatus = 'Skipped'; skipped += 1; continue; }
    const item = (state.items || []).find(i => metadataManagerItemId(i) === row.id) || row.item;
    const built = metadataBatchBuildPayload(item, row, fieldMap, mode);
    if (!Object.keys(built.payload).length) {
      row.applyStatus = 'Skipped - no selected changes';
      skipped += 1;
      continue;
    }
    entries.push({ row, item, built, id: row.id, payload: built.payload });
  }

  try {
    const result = await metadataManagerPersistBulk(entries);
    entries.forEach(entry => {
      const updatedItem = (state.items || []).find(i => metadataManagerItemId(i) === entry.id) || { ...entry.item, ...entry.built.payload };
      entry.row.item = updatedItem;
      entry.row.applyStatus = `Applied ${entry.built.changedLabels.length} field(s)`;
      entry.row.checked = false;
    });
    state.metadataManager.dirty = {};
    metadataManagerSetStatus(`Applied batch metadata to ${result.saved} row(s). Skipped ${skipped}. Failed ${failed}.`, failed ? 'error' : 'success');
  } catch (err) {
    failed = entries.length;
    entries.forEach(entry => {
      entry.row.applyStatus = `Save failed: ${err?.message || err}`;
      entry.row.checked = true;
    });
    metadataManagerSetStatus(`Batch metadata apply failed: ${err?.message || err}`, 'error');
  }

  renderMetadataManager();
  renderMetadataBatchSourceResults();
}

function metadataBatchSelectAllFields(source, checked = true) {
  document.querySelectorAll(`input[data-metadata-batch-source="${source}"][data-metadata-batch-field]`).forEach(input => { input.checked = checked; });
  renderMetadataBatchSourceResults();
}

async function metadataManagerNormalizeSelected() {
  const items = metadataManagerSelectedItems();
  if (!items.length) {
    metadataManagerSetStatus('Select rows to normalize first.', 'error');
    return;
  }
  metadataManagerSetStatus(`Normalizing ${items.length} selected row(s) in bulk batches...`);
  let titleUpdates = 0;
  const entries = items.map(item => {
    const beforeName = metadataManagerItemName(item);
    const payload = metadataManagerNormalizedPayload(item);
    const afterName = payload.title || beforeName;
    if (String(afterName || '').trim() && String(afterName || '').trim() !== String(beforeName || '').trim()) titleUpdates += 1;
    return { id: metadataManagerItemId(item), item, payload };
  });
  const result = await metadataManagerPersistBulk(entries);
  metadataManagerSetStatus(`Normalized ${result.saved} selected row(s). Updated ${titleUpdates} title/name value(s), filled missing language as English, kept region as US, and cleaned duplicate topic/tag values.`, 'success');
  renderMetadataManager();
}

function metadataManagerCsvValue(value) {
  const text = String(value ?? '');
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

function metadataManagerSyncFiltersFromControls() {
  state.metadataManager = state.metadataManager || {};
  const mappings = [
    ['metadataManagerSearch', 'search'],
    ['metadataManagerStatusFilter', 'statusFilter'],
    ['metadataManagerMissing', 'missing'],
    ['metadataManagerCategory', 'category']
  ];
  mappings.forEach(([id, field]) => {
    const control = $(id);
    if (control) state.metadataManager[field] = control.value || '';
  });
  const checkedKinds = Array.from(document.querySelectorAll('[data-metadata-manager-kind]:checked')).map(el => el.dataset.metadataManagerKind || '').filter(Boolean);
  metadataManagerSetSelectedKinds(checkedKinds.length ? checkedKinds : state.metadataManager.kindFilters || METADATA_MANAGER_KIND_FILTERS);
  return state.metadataManager;
}

function metadataManagerExportScopeSlug(kind = '') {
  const cleaned = String(kind || '').trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');
  return cleaned || 'all-metadata';
}

function metadataManagerExportCsv() {
  const manager = metadataManagerSyncFiltersFromControls();
  const requestedKinds = metadataManagerSelectedKinds();
  const requestedKind = requestedKinds.length === 1 ? requestedKinds[0] : '';
  let rows = metadataManagerEditableItems();

  // Hard guard the export scope from the live Type checkboxes. If the manager state ever
  // gets stale, this prevents a Strategy Guide export from falling back to magazines/all rows.
  const requestedSet = new Set(requestedKinds);
  rows = rows.filter(item => requestedSet.has(String(item?.kind || '').trim()));

  const columns = metadataManagerVisibleColumns();
  if (!rows.length) {
    const scopeText = requestedKind || requestedKinds.join(', ') || 'metadata';
    metadataManagerSetStatus(`No ${scopeText} row(s) match the current Metadata Manager filters.`, 'error');
    return;
  }
  const header = ['id', ...columns.map(column => column.key)];
  const lines = [header.join(',')];
  rows.forEach(item => {
    const values = [metadataManagerItemId(item), ...columns.map(column => metadataManagerFieldValue(item, column.key))];
    lines.push(values.map(metadataManagerCsvValue).join(','));
  });
  const csvText = lines.join('\r\n');
  // Prefix UTF-8 CSV exports with a BOM so Excel opens em dashes, degree symbols,
  // accented titles, and curly punctuation without mojibake.
  const blob = new Blob([new Uint8Array([0xEF, 0xBB, 0xBF]), csvText], { type: 'text/csv;charset=utf-8' });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  const scope = metadataManagerExportScopeSlug(requestedKind || requestedKinds.join('-'));
  link.download = `guidevault-metadata-${scope}-${new Date().toISOString().slice(0,10)}.csv`;
  document.body.appendChild(link);
  link.click();
  URL.revokeObjectURL(link.href);
  link.remove();
  const kinds = Array.from(new Set(rows.map(item => String(item?.kind || '').trim()).filter(Boolean))).sort();
  const scopeLabel = requestedKind || (kinds.length === 1 ? kinds[0] : 'current filtered metadata');
  metadataManagerSetStatus(`Exported ${rows.length} ${scopeLabel} row(s) with ${columns.length} visible column(s) to CSV.`, 'success');
}

function metadataManagerImportNormalizeKey(value) {
  return String(value || '')
    .toLowerCase()
    .replace(/&/g, ' and ')
    .replace(/[^a-z0-9]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function metadataManagerImportIssueKey(value) {
  const text = String(value || '').trim();
  if (!text) return '';
  const match = text.match(/(?:issue\s*#?|#)\s*0*([0-9]+[A-Za-z]?)\b/i) || text.match(/^0*([0-9]+[A-Za-z]?)$/);
  if (!match) return metadataManagerImportNormalizeKey(text);
  return String(match[1] || '').toLowerCase();
}

function metadataManagerImportMagazineTitleKey(itemOrEntry) {
  const candidates = [
    itemOrEntry?.magazineTitle,
    itemOrEntry?.series,
    itemOrEntry?.publication,
    itemOrEntry?.name,
    itemOrEntry?.title
  ];
  const raw = candidates.find(value => String(value || '').trim()) || '';
  return metadataManagerImportNormalizeKey(String(raw)
    .replace(/[\u2012\u2013\u2014-]\s*Issue\s*#?\s*\d+.*$/i, '')
    .replace(/\s*#\s*\d+.*$/i, '')
    .replace(/\s*Issue\s*#?\s*\d+.*$/i, ''));
}

function metadataManagerImportMagazineIssueKey(itemOrEntry) {
  const direct = metadataManagerImportIssueKey(itemOrEntry?.issueNumber || itemOrEntry?.magazineIssueNumber || itemOrEntry?.number);
  if (direct) return direct;
  return metadataManagerImportIssueKey(itemOrEntry?.name || itemOrEntry?.title || itemOrEntry?.fileName || itemOrEntry?.path);
}

function metadataManagerImportTitleKey(itemOrEntry) {
  const value = itemOrEntry?.manualTitle || itemOrEntry?.gameTitle || itemOrEntry?.magazineTitle || itemOrEntry?.title || itemOrEntry?.name || itemOrEntry?.fileName || itemOrEntry?.path || '';
  return metadataManagerImportNormalizeKey(value);
}

function metadataManagerBuildImportLookup() {
  const byId = new Map();
  const magazineByIssue = new Map();
  const titleByKind = new Map();

  (state.items || []).forEach(item => {
    const id = metadataManagerItemId(item);
    if (id && !byId.has(id)) byId.set(id, item);

    const kind = String(item?.kind || '').trim().toLowerCase();
    const titleKey = metadataManagerImportTitleKey(item);
    if (titleKey) {
      const scopedKey = `${kind}|${titleKey}`;
      if (!titleByKind.has(scopedKey)) titleByKind.set(scopedKey, []);
      titleByKind.get(scopedKey).push(item);
      const unscopedKey = `|${titleKey}`;
      if (!titleByKind.has(unscopedKey)) titleByKind.set(unscopedKey, []);
      titleByKind.get(unscopedKey).push(item);
    }

    if (kind === 'magazine') {
      const magazineKey = metadataManagerImportMagazineTitleKey(item);
      const issueKey = metadataManagerImportMagazineIssueKey(item);
      if (magazineKey && issueKey) {
        const key = `${magazineKey}|${issueKey}`;
        if (!magazineByIssue.has(key)) magazineByIssue.set(key, []);
        magazineByIssue.get(key).push(item);
      }
    }
  });

  return { byId, magazineByIssue, titleByKind };
}

function metadataManagerFindImportItem(entry, lookup = null) {
  const indexes = lookup || metadataManagerBuildImportLookup();
  const id = String(entry?.id || entry?.Id || '').trim();
  if (id && indexes.byId.has(id)) return indexes.byId.get(id);

  const entryKind = String(entry?.kind || entry?.type || '').trim().toLowerCase();
  const entryMagazineKey = metadataManagerImportMagazineTitleKey(entry);
  const entryIssueKey = metadataManagerImportMagazineIssueKey(entry);
  if (entryIssueKey && entryMagazineKey) {
    const exact = indexes.magazineByIssue.get(`${entryMagazineKey}|${entryIssueKey}`) || [];
    if (exact.length === 1) return exact[0];
    if (exact.length > 1) return exact[0];

    for (const [key, matches] of indexes.magazineByIssue.entries()) {
      const [itemMagazineKey, itemIssueKey] = key.split('|');
      if (itemIssueKey !== entryIssueKey) continue;
      if (itemMagazineKey === entryMagazineKey || itemMagazineKey.includes(entryMagazineKey) || entryMagazineKey.includes(itemMagazineKey)) {
        if (matches.length) return matches[0];
      }
    }
  }

  const entryTitleKey = metadataManagerImportTitleKey(entry);
  if (entryTitleKey) {
    const scoped = indexes.titleByKind.get(`${entryKind}|${entryTitleKey}`) || [];
    if (scoped.length === 1) return scoped[0];
    const unscoped = indexes.titleByKind.get(`|${entryTitleKey}`) || [];
    if (unscoped.length === 1) return unscoped[0];
  }

  return null;
}

function metadataManagerImportJsonFile(file) {
  if (!file) return;
  const reader = new FileReader();
  reader.onload = async () => {
    try {
      const parsed = JSON.parse(String(reader.result || '[]'));
      const entries = Array.isArray(parsed) ? parsed : (Array.isArray(parsed.items) ? parsed.items : []);
      const validFields = metadataManagerValidColumnKeys();
      const lookup = metadataManagerBuildImportLookup();
      const importAliases = {
        title: 'name',
        Name: 'name',
        system: 'category',
        primarySystem: 'category',
        tags: 'topics',
        language: 'languageTag',
        metadataPageCount: 'pageCount'
      };
      let matched = 0;
      let staged = 0;
      let changedFields = 0;
      for (let entryIndex = 0; entryIndex < entries.length; entryIndex += 1) {
        const entry = entries[entryIndex];
        const item = metadataManagerFindImportItem(entry, lookup);
        if (!item) {
          if (entryIndex > 0 && entryIndex % 250 === 0) await metadataManagerYieldToBrowser();
          continue;
        }
        const id = metadataManagerItemId(item);
        if (!id) continue;
        matched += 1;
        const changes = {};
        Object.entries(entry || {}).forEach(([rawField, rawValue]) => {
          if (['id','Id','schema','generatedAt','items','matchName','sourceName','originalName'].includes(rawField)) return;
          const field = importAliases[rawField] || rawField;
          if (!validFields.has(field) || METADATA_MANAGER_READONLY_COLUMNS.has(field)) return;
          let value = rawValue;
          if (field === 'metadataStatus') value = normalizeMetadataStatus(rawValue);
          else if (METADATA_MANAGER_ARRAY_FIELDS.has(field) || field === 'topics') value = Array.isArray(rawValue) ? rawValue.join(', ') : String(rawValue ?? '');
          else value = String(rawValue ?? '');
          if (String(metadataManagerFieldValue(item, field) ?? '').trim() === String(value ?? '').trim()) return;
          changes[field] = value;
        });
        if (Object.keys(changes).length) {
          state.metadataManager.dirty[id] = { ...(state.metadataManager.dirty[id] || {}), ...changes };
          staged += 1;
          changedFields += Object.keys(changes).length;
        }
        if (entryIndex > 0 && entryIndex % 250 === 0) {
          metadataManagerSetStatus(`Import staging ${entryIndex}/${entries.length} record(s)...`);
          await metadataManagerYieldToBrowser();
        }
      }
      const unmatched = Math.max(0, entries.length - matched);
      metadataManagerSetStatus(`Imported ${staged} row(s) with ${changedFields} field change(s). Matched ${matched} of ${entries.length} import record(s)${unmatched ? `; ${unmatched} unmatched` : ''}. Review and click Save Edited Rows.`, staged || matched ? 'success' : 'error');
      if (staged > METADATA_MANAGER_DEFAULT_RENDER_LIMIT) metadataManagerResetRenderLimit();
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
  prepareLibraryItemComputedFields(updated);
  if (index >= 0) state.items[index] = updated;
  else state.items.push(updated);
  markLibraryIndexesDirty();
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
  return raw ? raw : '\u2014';
}

function firstDetailText(...values) {
  for (const value of values) {
    const raw = String(value ?? '').trim();
    if (raw) return raw;
  }
  return '';
}

function strategyGuideTitleForItem(item) {
  return firstDetailText(item?.strategyGuideTitle, item?.title, item?.name);
}

function strategyGameTitleForItem(item) {
  return firstDetailText(item?.gameTitle, item?.sortTitle, item?.franchise, item?.series, item?.platformMatchTitle);
}

function magazineTitleForItem(item) {
  return firstDetailText(item?.magazineTitle, item?.series, item?.title, item?.name) || 'Video Game Magazine';
}

function detailHeaderTitleForItem(item) {
  if (item?.kind === 'Strategy Guide') {
    return strategyGameTitleForItem(item) || strategyGuideTitleForItem(item) || displayTitle(item) || 'Strategy Guide';
  }
  if (item?.kind === 'Magazine') {
    return magazineTitleForItem(item);
  }
  return displayTitle(item);
}

function detailTagListHtml(value) {
  const values = itemArray(value);
  if (!values.length) return '<span class="muted-dash">\u2014</span>';
  return `<div class="overview-chip-list">${values.map(v => `<span class="overview-chip">${escapeHtml(v)}</span>`).join('')}</div>`;
}

function chipListHtml(value) {
  const values = itemArray(value);
  if (!values.length) return '<span class="muted-dash">\u2014</span>';
  return values.map(v => `<span class="overview-chip">${escapeHtml(v)}</span>`).join('');
}

function guidevaultCleanDisplayText(value) {
  return String(value ?? '')
    .replaceAll('\u00e2\u20ac\u201d', ' - ')
    .replaceAll('\u00e2\u20ac\u201c', ' - ')
    .replaceAll('\u00e2\u20ac\u00a2', ' - ')
    .replaceAll('\u00e2\u20ac\u00a6', '...')
    .replaceAll('\u2014', ' - ')
    .replaceAll('\u2013', ' - ')
    .replaceAll('\u2022', ' - ')
    .replaceAll('\u2026', '...')
    .replace(/\s+-\s+/g, ' - ')
    .trim();
}

function magazineOverviewField(label, value, className = '') {
  const displayValue = guidevaultCleanDisplayText(detailValue(value));
  return `<div class="overview-field ${className}"><span>${escapeHtml(label)}</span><strong>${escapeHtml(displayValue || '-')}</strong></div>`;
}

function magazineOverviewTagSection(label, value) {
  return `<section class="overview-tag-section"><h3>${escapeHtml(label)}</h3>${detailTagListHtml(value)}</section>`;
}

function magazineOverviewFieldIfValue(label, value, className = '') {
  const cleaned = guidevaultCleanDisplayText(value);
  return cleaned ? magazineOverviewField(label, cleaned, className) : '';
}

function valuesEqualText(a, b) {
  return String(a || '').trim().toLowerCase() === String(b || '').trim().toLowerCase();
}

function magazineIssueSubtitleParts(item) {
  const parts = [];
  const issue = String(item?.issueNumber || '').trim();
  const volume = String(item?.volume || '').trim();
  if (issue) parts.push(`Issue #${issue}`);
  if (volume) parts.push(`Volume ${volume}`);
  return parts;
}

function magazineIssueSubtitleText(item) {
  return magazineIssueSubtitleParts(item).join(' - ');
}

function magazineIssueSubtitleHtml(item) {
  const parts = magazineIssueSubtitleParts(item);
  return parts.length ? `<div class="magazine-hero-subtitle">${parts.map(escapeHtml).join(' <span> - </span> ')}</div>` : '';
}

function magazineDetailSubtitleText(item) {
  const primarySystem = String(item?.primarySystem || item?.system || detailSystemLabelForItem(item) || '').trim();
  const publisher = String(item?.publisher || '').trim();
  const coverDate = String(item?.coverDate || item?.publicationDate || '').trim();
  const pageCount = Number(item?.pageCount ?? item?.PageCount ?? 0) || 0;
  const pageCountText = pageCount > 0 ? `${pageCount} pages` : '';
  return [magazineIssueSubtitleText(item), coverDate, primarySystem, publisher, pageCountText]
    .map(v => guidevaultCleanDisplayText(v))
    .filter(Boolean)
    .join(' - ');
}

function magazineOverviewHtml(item) {
  const publicationTitle = guidevaultCleanDisplayText(item.magazineTitle || item.series || detectedSystemOf(item) || item.title || 'Video Game Magazine');
  const displayEntryTitle = guidevaultCleanDisplayText(item.title || '');
  const showEntryTitle = displayEntryTitle && !valuesEqualText(displayEntryTitle, publicationTitle);
  const coverDate = guidevaultCleanDisplayText(item.coverDate || item.publicationDate || item.year);
  const publicationDate = item.publicationDate && !valuesEqualText(item.publicationDate, item.coverDate) ? guidevaultCleanDisplayText(item.publicationDate) : '';
  const identityTags = [
    item.magazineCategory || 'Video Game Magazine',
    item.platformFocus || item.primarySystem || item.system,
    coverDate
  ].filter(Boolean);
  return `
    <div class="magazine-hero-overview magazine-hero-overview-refined">
      <div class="magazine-hero-kicker">Magazine</div>
      <h2>${escapeHtml(publicationTitle)}</h2>
      ${magazineIssueSubtitleHtml(item)}
      ${showEntryTitle ? `<p class="magazine-entry-title-note">${escapeHtml(displayEntryTitle)}</p>` : ''}
      <p>${escapeHtml(item.summary || descriptionFor(item))}</p>
      <div class="magazine-hero-tags">${identityTags.map(v => guidevaultCleanDisplayText(v)).filter(Boolean).map(v => `<span class="overview-chip">${escapeHtml(v)}</span>`).join('')}</div>
    </div>
    <div class="magazine-overview-grid magazine-overview-grid-refined">
      <section class="overview-card overview-card-identity magazine-overview-card-no-heading magazine-overview-primary-card">
        <div class="overview-field-grid">
          ${magazineOverviewFieldIfValue('Issue #', item.issueNumber)}
          ${magazineOverviewFieldIfValue('Volume', item.volume)}
          ${magazineOverviewField('Cover Date', coverDate)}
          ${magazineOverviewFieldIfValue('Publisher', item.publisher)}
          ${magazineOverviewFieldIfValue('Barcode / UPC / ISSN', item.barcodeUpcIssn || item.BarcodeUpcIssn || item.barcode || item.upc || item.issn)}
          ${magazineOverviewFieldIfValue('Page Count', item.pageCount || item.PageCount ? `${item.pageCount || item.PageCount} pages` : '')}
          ${magazineOverviewFieldIfValue('Editor', item.writer)}
          ${magazineOverviewFieldIfValue('Publication Date', publicationDate)}
          ${magazineOverviewFieldIfValue('Entry Title', showEntryTitle ? displayEntryTitle : '')}
        </div>
      </section>
      <section class="overview-card overview-card-classification magazine-overview-card-no-heading magazine-overview-context-card">
        <div class="overview-field-grid">
          ${magazineOverviewField('Country of publication', item.region)}
          ${magazineOverviewField('Language', item.languageTag)}
          ${magazineOverviewField('Platform / Audience Focus', item.platformFocus)}
          ${magazineOverviewField('Cover Game / Subject', item.coverSubject, 'magazine-overview-second-row magazine-overview-cover-subject')}
          ${magazineOverviewField('Category', item.magazineCategory || item.category || 'Video game magazine', 'magazine-overview-second-row magazine-overview-category')}
        </div>
      </section>
      ${magazineOverviewTagSection('Featured Games', item.featuredGames)}
      ${magazineOverviewTagSection('Featured Platforms', item.featuredPlatforms)}
      ${magazineOverviewTagSection('Sections / Departments', item.specialFeatures)}
      ${magazineOverviewTagSection('Insert Details', item.includedExtras)}
    </div>`;
}


function manualOverviewHtml(item) {
  const gameTitle = item.gameTitle || item.platformMatchTitle || item.series || item.title || '';
  const manualTitle = item.manualTitle || item.title || '';
  const manualType = item.manualType || 'Instruction Manual';
  const systemLabel = detailSystemLabelForItem(item);
  const identityTags = [manualType, systemLabel, item.gameReleaseYear || item.year].filter(Boolean);
  const rating = String(item?.rating || '').trim();
  const esrbLabel = esrbDisplayLabel(rating);
  const esrbHtml = `<img class="manual-hero-esrb" src="${escapeHtml(esrbIconUrl(rating))}" alt="${escapeHtml(esrbLabel)}" title="${escapeHtml(esrbLabel)}" onerror="this.onerror=null;this.src='/assets/ESRB/RatedNone.png';" />`;
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
          ${magazineOverviewField('Game Title', gameTitle, 'wide')}
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
            ${magazineOverviewField('Associated Game', gameTitle, 'wide')}
            ${magazineOverviewField('Game Franchise / Series', item.franchise || item.series)}
            ${magazineOverviewField('Game Developer', item.developer)}
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
          ${magazineOverviewTagSection('Physical Extras', item.includedExtras)}
          ${magazineOverviewTagSection('Characters Covered', item.charactersCovered)}
          ${magazineOverviewTagSection('Items / Mechanics', item.itemsCovered)}
          ${magazineOverviewField('Controls / Scheme', item.controlScheme, 'wide')}
          ${magazineOverviewField('Warranty / Support', item.warrantySupport, 'wide')}
        </div>
      </section>
    </div>`;
}


function strategyOverviewHtml(item) {
  const guideTitle = strategyGuideTitleForItem(item);
  const gameTitle = strategyGameTitleForItem(item);
  const systemLabel = detailSystemLabelForItem(item);
  const identityTags = [item.guideType || 'Strategy Guide', systemLabel, item.year].filter(Boolean);
  const rating = String(item?.rating || '').trim();
  const showEsrb = isEsrbIconEligible(item);
  const esrbLabel = esrbDisplayLabel(rating);
  const esrbHtml = showEsrb
    ? `<img class="strategy-hero-esrb" src="${escapeHtml(esrbIconUrl(rating))}" alt="${escapeHtml(esrbLabel)}" title="${escapeHtml(esrbLabel)}" onerror="this.onerror=null;this.src='/assets/ESRB/RatedNone.png';" />`
    : '';
  return `
    <div class="strategy-hero-overview${showEsrb ? ' has-hero-esrb' : ''}">
      <div class="strategy-hero-copy">
        <div class="strategy-hero-kicker">Strategy Guide</div>
        <h2>${escapeHtml(guideTitle || gameTitle || 'Strategy Guide')}</h2>
        <p>${escapeHtml(item.summary || descriptionFor(item))}</p>
        <div class="strategy-hero-tags">${identityTags.map(v => `<span class="overview-chip">${escapeHtml(v)}</span>`).join('')}</div>
      </div>
      ${esrbHtml}
    </div>
    <div class="strategy-overview-board">
      <section class="overview-card strategy-quick-card">
        <div class="strategy-snapshot-grid">
          ${magazineOverviewField('System', systemLabel)}
          ${magazineOverviewField('Publisher', item.publisher)}
          ${magazineOverviewField('Author', item.writer)}
          ${magazineOverviewField('Year', item.year)}
          ${magazineOverviewField('Page Count', itemPageCountLabel(item))}
          ${magazineOverviewField('Edition', item.edition)}
          ${magazineOverviewField('Region', item.region)}
          ${magazineOverviewField('Language', item.languageTag)}
        </div>
      </section>
      <div class="strategy-context-stack">
        <section class="overview-card strategy-game-card">
          <div class="strategy-context-list">
            ${magazineOverviewField('Game Franchise / Series', item.franchise || item.series)}
            ${magazineOverviewField('Game Developer', item.developer)}
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
          ${magazineOverviewTagSection('Physical Extras', item.includedExtras)}
          ${magazineOverviewTagSection('Covered Games', item.coveredGames && item.coveredGames.length ? item.coveredGames : (gameTitle ? [gameTitle] : []))}
          ${magazineOverviewTagSection('Covered Platforms', item.coveredPlatforms && item.coveredPlatforms.length ? item.coveredPlatforms : item.associatedPlatforms)}
          ${magazineOverviewTagSection('Characters Covered', item.charactersCovered)}
          ${magazineOverviewTagSection('Locations Covered', item.locationsCovered)}
        </div>
      </section>
    </div>`;
}

function magazineTechnicalRows(item) {
  const sourceFile = item.fileName || (item.path ? String(item.path).split(/[\\/]/).pop() : '\u2014');
  const libraryPath = item.libraryName || item.libraryType || '\u2014';
  const scanStatus = item.validationStatus && item.validationStatus !== 'ok' ? item.validationStatus : 'OK';
  const fileSize = Number(item.sizeBytes || 0) > 0 ? `${Math.round(Number(item.sizeBytes) / 1024 / 1024 * 10) / 10} MB` : '\u2014';
  const modified = item.modified ? new Date(item.modified).toLocaleString() : '\u2014';
  return [
    ['Page Count', itemPageCountLabel(item)],
    ['File Format', item.format || '\u2014'],
    ['Source File', sourceFile],
    ['Library', libraryPath],
    ['Source Path', item.path || '\u2014'],
    ['File Size', fileSize],
    ['Modified Date', modified],
    ['Scan Status', scanStatus],
    ['Metadata Status', metadataStatusOf(item)],
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
  } else {
    panel.innerHTML = `
      <dl class="metadata-technical-list library-data-flat-list">
        ${rows.map(([k, v]) => metaRow(k, v, false)).join('')}
      </dl>`;
  }
  resetMetadataCoverPicker(item);
}

const GUIDEVAULT_COVER_PICKER_INITIAL_LIMIT = 72;

function metadataCoverPickerItemId(item = state.selected) {
  return String(item?.id || item?.Id || '').trim();
}

function resetMetadataCoverPicker(item) {
  const panel = $('metadataCoverPicker');
  if (!panel) return;
  const id = metadataCoverPickerItemId(item);
  const format = String(item?.format || item?.Format || '').toUpperCase();
  if (!id || format === 'PDF') {
    panel.classList.add('hidden');
    panel.innerHTML = '';
    return;
  }
  panel.classList.remove('hidden');
  panel._coverPickerData = null;
  panel.dataset.itemId = id;
  panel.dataset.loaded = '0';
  panel.innerHTML = `
    <div class="metadata-cover-picker-head">
      <div>
        <h4>Cover Page Override</h4>
        <p class="sub">Click Load Pages only when you want to inspect this archive and choose an exact cover page.</p>
      </div>
      <div class="metadata-cover-picker-actions">
        <button class="ghost tiny" type="button" data-cover-picker-load>Load Pages</button>
        <button class="ghost tiny" type="button" data-cover-picker-clear disabled>Use Auto Cover</button>
        <button class="ghost tiny danger" type="button" data-cover-picker-delete disabled>Delete Highlighted Page</button>
      </div>
    </div>
    <div class="metadata-cover-picker-status">Cover page choices are loaded on demand so normal library cover loading stays fast.</div>`;
  // Do not enumerate archive pages automatically. The cover picker can be
  // expensive for large CBR/CBZ files or network-hosted libraries, so it only
  // loads after the user clicks Load Pages.
}


function coverPickerPageLabel(entry = {}) {
  const n = Number(entry.index || 0) + 1;
  return `Page ${String(n).padStart(3, '0')}`;
}

function renderCoverPickerEntries(data = {}, showAll = false) {
  const panel = $('metadataCoverPicker');
  if (!panel) return;
  const entries = Array.isArray(data.entries) ? data.entries : [];
  const id = String(data.itemId || metadataCoverPickerItemId()).trim();
  const visible = showAll ? entries : entries.slice(0, GUIDEVAULT_COVER_PICKER_INITIAL_LIMIT);
  const selectedIndex = Number(data.selectedIndex ?? -1);
  const selectedEntry = selectedIndex >= 0 ? entries[selectedIndex] : entries.find(entry => entry?.isSelected);
  const hasHighlightedPage = !!selectedEntry;
  const canDeletePages = data.canDeletePages === true;
  const selectedText = data.hasManualOverride && selectedIndex >= 0
    ? `Manual cover: page ${selectedIndex + 1} / ${entries.length}. Delete Highlighted Page will remove that selected archive image after confirmation.`
    : `Automatic cover detection is active. ${entries.length ? 'Select a page below to override it; the selected page can then be deleted if needed.' : ''}`;
  const deleteDisabled = !(entries.length && hasHighlightedPage && canDeletePages);
  const deleteTitle = canDeletePages
    ? (hasHighlightedPage ? `Delete ${selectedEntry?.fileName || selectedEntry?.entryKey || 'selected page'}` : 'Select/highlight a page before deleting.')
    : (data.deletePageHint || 'Page deletion is available only for writable CBZ/ZIP archives.');

  panel.classList.remove('hidden');
  panel.dataset.itemId = id;
  panel.dataset.loaded = '1';
  panel.dataset.showAll = showAll ? '1' : '0';
  panel._coverPickerData = data;
  panel.innerHTML = `
    <div class="metadata-cover-picker-head">
      <div>
        <h4>Cover Page Override</h4>
        <p class="sub">${escapeHtml(selectedText)}</p>
      </div>
      <div class="metadata-cover-picker-actions">
        <button class="ghost tiny" type="button" data-cover-picker-load>Reload Pages</button>
        <button class="ghost tiny" type="button" data-cover-picker-clear${data.hasManualOverride ? '' : ' disabled'}>Use Auto Cover</button>
        <button class="ghost tiny danger" type="button" data-cover-picker-delete${deleteDisabled ? ' disabled' : ''} title="${escapeForAttribute(deleteTitle)}">Delete Highlighted Page</button>
      </div>
    </div>
    <div class="metadata-cover-picker-status">${escapeHtml(entries.length ? `${entries.length} image page(s) found in this archive. ${data.deletePageHint || ''}`.trim() : 'No image pages found in this archive.')}</div>
    ${entries.length ? `<div class="metadata-cover-picker-grid">
      ${visible.map(entry => `
        <button class="metadata-cover-choice${entry.isSelected ? ' selected' : ''}" type="button" data-cover-picker-select="${escapeForAttribute(entry.entryKey || '')}" data-cover-picker-page="${escapeForAttribute(entry.index)}" title="${escapeForAttribute(entry.entryKey || '')}">
          <span class="metadata-cover-choice-img"><img src="${escapeForAttribute((entry.thumbnailUrl || '').replace('/api/items/', `/api/items/`))}" alt="${escapeForAttribute(coverPickerPageLabel(entry))}" loading="lazy" onerror="this.onerror=null;this.src='/assets/missing-cover.svg';" /></span>
          <strong>${escapeHtml(coverPickerPageLabel(entry))}</strong>
          <em>${escapeHtml(entry.fileName || entry.entryKey || '')}</em>
        </button>`).join('')}
    </div>` : ''}
    ${entries.length > visible.length ? `<button class="ghost tiny metadata-cover-picker-show-all" type="button" data-cover-picker-show-all>Show All ${entries.length} Pages</button>` : ''}`;
}

async function loadCoverPickerForSelected(force = false, showAll = false) {
  const panel = $('metadataCoverPicker');
  const item = state.selected;
  const id = metadataCoverPickerItemId(item);
  if (!panel || !id || state.activeTab !== 'library-data') return;
  if (String(item?.format || '').toUpperCase() === 'PDF') return;
  if (!force && panel.dataset.itemId === id && panel.dataset.loaded === '1') return;
  panel.classList.remove('hidden');
  panel.dataset.itemId = id;
  panel.innerHTML = `
    <div class="metadata-cover-picker-head"><div><h4>Cover Page Override</h4><p class="sub">Reading image entries from the archive...</p></div></div>
    <div class="metadata-cover-picker-status">Loading cover choices...</div>`;
  try {
    const res = await fetch(`/api/items/${encodeURIComponent(id)}/cover-options`, { cache: 'no-store' });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || `Cover options failed. HTTP ${res.status}`);
    renderCoverPickerEntries(data, showAll);
  } catch (err) {
    console.error('Cover picker load failed', err);
    panel.innerHTML = `
      <div class="metadata-cover-picker-head"><div><h4>Cover Page Override</h4><p class="sub">Unable to read cover pages.</p></div><div class="metadata-cover-picker-actions"><button class="ghost tiny" type="button" data-cover-picker-load>Try Again</button></div></div>
      <div class="metadata-cover-picker-status error">${escapeHtml(err?.message || String(err || 'Unknown error'))}</div>`;
  }
}

function refreshSelectedCoverImages() {
  const item = state.selected;
  if (!item) return;
  const newDetailCover = $('detailCover');
  if (newDetailCover) newDetailCover.src = coverUrl(item, { width: 560 });
  document.querySelectorAll(`img[data-cover-src]`).forEach(img => {
    const card = img.closest?.('[data-id]');
    const cardId = card?.dataset?.id || '';
    if (cardId && cardId !== metadataCoverPickerItemId(item)) return;
    const next = coverUrl(item, { width: Number(img.dataset.coverWidth || GUIDEVAULT_GRID_COVER_THUMB_WIDTH) || GUIDEVAULT_GRID_COVER_THUMB_WIDTH });
    img.dataset.coverSrc = next;
    img.src = next;
  });
}

async function saveCoverPickerSelection(entryKey = '', pageIndex = null) {
  const id = metadataCoverPickerItemId();
  if (!id || !entryKey) return;
  const panel = $('metadataCoverPicker');
  try {
    if (panel) panel.querySelectorAll('button').forEach(btn => { btn.disabled = true; });
    const res = await fetch(`/api/items/${encodeURIComponent(id)}/cover-selection`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ entryKey, pageIndex })
    });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || `Cover save failed. HTTP ${res.status}`);
    setCoverOverrideBust(id, data?.selectedUpdatedAt || Date.now());
    if (state.selected) state.selected.coverOverrideBust = readCoverOverrideBustMap()[id];
    setStatus(data?.message || 'Cover override saved.');
    refreshSelectedCoverImages();
    await loadCoverPickerForSelected(true, panel?.dataset?.showAll === '1');
  } catch (err) {
    console.error('Cover selection failed', err);
    setStatus(`Unable to save cover override: ${err?.message || err}`);
    if (panel) {
      const status = panel.querySelector('.metadata-cover-picker-status');
      if (status) {
        status.textContent = `Unable to save cover override: ${err?.message || err}`;
        status.classList.add('error');
      }
    }
  }
}

async function clearCoverPickerSelection() {
  const id = metadataCoverPickerItemId();
  if (!id) return;
  const panel = $('metadataCoverPicker');
  try {
    if (panel) panel.querySelectorAll('button').forEach(btn => { btn.disabled = true; });
    const res = await fetch(`/api/items/${encodeURIComponent(id)}/cover-selection`, { method: 'DELETE', cache: 'no-store' });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || `Cover clear failed. HTTP ${res.status}`);
    setCoverOverrideBust(id, Date.now());
    if (state.selected) state.selected.coverOverrideBust = readCoverOverrideBustMap()[id];
    setStatus(data?.message || 'Manual cover override cleared.');
    refreshSelectedCoverImages();
    await loadCoverPickerForSelected(true, panel?.dataset?.showAll === '1');
  } catch (err) {
    console.error('Cover override clear failed', err);
    setStatus(`Unable to clear cover override: ${err?.message || err}`);
  }
}

async function deleteCoverPickerHighlightedPage() {
  const id = metadataCoverPickerItemId();
  const panel = $('metadataCoverPicker');
  const data = panel?._coverPickerData || {};
  const entries = Array.isArray(data.entries) ? data.entries : [];
  const selectedIndex = Number(data.selectedIndex ?? -1);
  const selectedEntry = selectedIndex >= 0 ? entries[selectedIndex] : entries.find(entry => entry?.isSelected);
  if (!id || !selectedEntry?.entryKey) {
    setStatus('Select a cover page before deleting a page.');
    return;
  }
  if (data.canDeletePages !== true) {
    setStatus(data.deletePageHint || 'Page deletion is available only for writable CBZ/ZIP archives.');
    return;
  }
  const pageNumber = Number(selectedEntry.index ?? selectedIndex ?? 0) + 1;
  const fileName = selectedEntry.fileName || selectedEntry.entryKey || `page ${pageNumber}`;
  const ok = window.confirm(`Delete this page from the source archive?\n\nPage ${pageNumber}: ${fileName}\n\nThis permanently rewrites the CBZ/ZIP file. This cannot be undone from Guidevault.`);
  if (!ok) return;

  try {
    if (panel) panel.querySelectorAll('button').forEach(btn => { btn.disabled = true; });
    const res = await fetch(`/api/items/${encodeURIComponent(id)}/archive-page`, {
      method: 'DELETE',
      headers: { 'Content-Type': 'application/json' },
      cache: 'no-store',
      body: JSON.stringify({ entryKey: selectedEntry.entryKey, pageIndex: selectedEntry.index })
    });
    let result = null;
    try { result = await res.json(); } catch {}
    if (!res.ok) throw new Error(result?.error || `Page delete failed. HTTP ${res.status}`);
    setCoverOverrideBust(id, result?.selectedUpdatedAt || Date.now());
    if (state.selected) {
      state.selected.coverOverrideBust = readCoverOverrideBustMap()[id];
      if (Number(state.selected.pageCount || state.selected.PageCount || 0) > 0) {
        state.selected.pageCount = Math.max(0, Number(state.selected.pageCount || state.selected.PageCount || 0) - 1);
        state.selected.PageCount = state.selected.pageCount;
      }
    }
    setStatus(result?.message || 'Page deleted.');
    refreshSelectedCoverImages();
    await loadCoverPickerForSelected(true, panel?.dataset?.showAll === '1');
  } catch (err) {
    console.error('Cover picker page delete failed', err);
    setStatus(`Unable to delete page: ${err?.message || err}`);
    if (panel) {
      const status = panel.querySelector('.metadata-cover-picker-status');
      if (status) {
        status.textContent = `Unable to delete page: ${err?.message || err}`;
        status.classList.add('error');
      }
      panel.querySelectorAll('button').forEach(btn => { btn.disabled = false; });
    }
  }
}

function handleMetadataCoverPickerClick(e) {
  const load = e.target.closest?.('[data-cover-picker-load]');
  if (load) { e.preventDefault(); loadCoverPickerForSelected(true); return; }
  const clear = e.target.closest?.('[data-cover-picker-clear]');
  if (clear && !clear.disabled) { e.preventDefault(); clearCoverPickerSelection(); return; }
  const del = e.target.closest?.('[data-cover-picker-delete]');
  if (del && !del.disabled) { e.preventDefault(); deleteCoverPickerHighlightedPage(); return; }
  const showAll = e.target.closest?.('[data-cover-picker-show-all]');
  if (showAll) {
    e.preventDefault();
    const panel = $('metadataCoverPicker');
    if (panel?._coverPickerData) renderCoverPickerEntries(panel._coverPickerData, true);
    else loadCoverPickerForSelected(true, true);
    return;
  }
  const choice = e.target.closest?.('[data-cover-picker-select]');
  if (choice) {
    e.preventDefault();
    saveCoverPickerSelection(choice.dataset.coverPickerSelect || '', Number(choice.dataset.coverPickerPage || -1));
  }
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
    none: 'RatedNone.png'
  }[key] || 'RatedNone.png';
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
    if (!icon.src.endsWith('/RatedNone.png')) icon.src = '/assets/ESRB/RatedNone.png';
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

function uniqueItemIds(ids = []) {
  const seen = new Set();
  return ids.map(id => String(id || '').trim()).filter(id => {
    if (!id || seen.has(id)) return false;
    seen.add(id);
    return true;
  });
}

function detailNavigationIdsFromContainer(card, item = null) {
  const currentId = String(card?.dataset?.id || itemIdOf(item) || '').trim();

  // Virtualized grids only keep the visible card window in the DOM.  When a
  // Details page is opened from the main grid, use the full filtered library
  // order so Previous/Next can navigate beyond the currently rendered card slice.
  if (card?.closest?.('#grid') && Array.isArray(state.filtered) && state.filtered.length) {
    const filteredIds = uniqueItemIds(state.filtered.map(itemIdOf));
    if (!currentId || filteredIds.includes(currentId)) return filteredIds;
  }

  const root = card?.closest?.('#recentGrid, .home-shelf, .home-shelf-row, .collection-row, #libraryView, .library-grid-scroll') || $('libraryView') || document;
  return uniqueItemIds([...root.querySelectorAll('article.card[data-id], .card[data-id]')]
    .map(el => el.dataset.id));
}

function setDetailNavigationContextFromCard(card, item = null) {
  const id = String(card?.dataset?.id || itemIdOf(item) || '').trim();
  if (!id) return;
  const ids = detailNavigationIdsFromContainer(card, item);
  if (ids.includes(id)) {
    state.detailNavigationIds = ids;
  }
}

function detailNavigationSource(item) {
  if (!item) return [];
  const currentId = itemIdOf(item);
  const currentKind = String(item.kind || '').trim();
  const byId = new Map((state.items || []).map(i => [itemIdOf(i), i]));

  // First choice: use the card order captured when the user opened Details.
  // This avoids Previous/Next changing direction or breaking after the library
  // grid is hidden, re-rendered, searched, or sorted.
  const contextIds = Array.isArray(state.detailNavigationIds) ? state.detailNavigationIds : [];
  if (contextIds.includes(currentId)) {
    const contextItems = contextIds
      .map(id => byId.get(id))
      .filter(i => i && String(i.kind || '').trim() === currentKind);
    if (contextItems.some(i => itemIdOf(i) === currentId)) return contextItems;
  }

  // Fallback: use the current filtered list order.
  let source = Array.isArray(state.filtered)
    ? state.filtered.filter(i => String(i.kind || '').trim() === currentKind)
    : [];

  if (!source.some(i => itemIdOf(i) === currentId)) {
    source = (Array.isArray(state.items) ? state.items : [])
      .filter(i => String(i.kind || '').trim() === currentKind)
      .sort(compareDetailSequence);
  }

  const seen = new Set();
  return source.filter(i => {
    const id = itemIdOf(i);
    if (!id || seen.has(id)) return false;
    seen.add(id);
    return true;
  });
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
  $('detailCover').src = coverUrl(item, { width: 560 });
  applyColorscapeToDetail(item);
  if ($('readBtn')) $('readBtn').dataset.itemId = item.id || '';
  $('detailCover').classList.toggle('nes-detail-cover', specialCardClass(item).includes('nes-manual-card'));
  $('detailTitle').textContent = detailHeaderTitleForItem(item);
  updateDetailNavigationButtons(item);
  const detectedSystem = detailSystemLabelForItem(item);
  if (item.kind === 'Magazine') {
    $('detailSub').textContent = magazineDetailSubtitleText(item) || [detectedSystem, item.coverDate || item.year || 'Unknown'].map(v => guidevaultCleanDisplayText(v)).filter(Boolean).join(' - ');
  } else if (item.kind === 'Strategy Guide') {
    const strategyGameYear = guidevaultCleanDisplayText(item.gameReleaseYear || item.GameReleaseYear || '');
    $('detailSub').textContent = [detectedSystem, strategyGameYear || 'Unknown'].filter(Boolean).join(' - ');
  } else {
    $('detailSub').textContent = [detectedSystem, item.year || 'Unknown'].filter(Boolean).join(' - ');
  }
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
      ['Publisher', item.publisher || '\u2014'],
      ['Year', item.year || '\u2014'],
      ['Writer', item.writer || '\u2014'],
      ['Pages', itemPageCountLabel(item)]
    ];
    if (metaEl) {
      metaEl.className = 'detail-meta-list';
      metaEl.innerHTML = metaRows.map(([k, v, html]) => metaRow(k, v, !!html)).join('');
    }
  }
  updateMetadataTechnicalInfo(item);
  renderDetailReviews(item);
  loadPublicReviewsForItem(item);
  $('editTitle').value = item.title || '';
  $('editKind').value = item.kind || 'Manual';
  if ($('editMetadataStatus')) $('editMetadataStatus').value = metadataStatusOf(item);
  $('editCategory').value = item.kind === 'Magazine' ? '' : (preferredPlatformOf(item) || categoryOf(item) || '');
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
  setMaybeValue('editBarcodeUpcIssn', isMagazine ? (item.barcodeUpcIssn || item.BarcodeUpcIssn || item.barcode || item.upc || item.issn || '') : '');
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
  setMaybeValue('editPublicationDateGuide', isStrategyGuide ? item.publicationDate : '');
  setMaybeValue('editDeveloper', (isStrategyGuide || isManual) ? item.developer : '');
  setMaybeValue('editGamePublisher', (isStrategyGuide || isManual) ? item.gamePublisher : '');
  setMaybeValue('editGameReleaseYear', (isStrategyGuide || isManual) ? (item.gameReleaseYear || (isManual ? item.year : '')) : '');
  setMaybeValue('editGenre', (isStrategyGuide || isManual) ? item.genre : '');
  $('editYear').value = (isManual || isStrategyGuide) ? '' : (item.year || '');
  if ($('editPageCount')) $('editPageCount').value = item.metadataPageCount || item.pageCountMetadata || item.pageCountEntered || item.pageCount || item.PageCount || '';
  $('editWriter').value = item.writer || '';
  $('editSummary').value = item.summary || '';
  setMaybeValue('editFeaturedGames', isMagazine ? itemList(item.featuredGames).replace(/^\u2014$/, '') : '');
  setMaybeValue('editFeaturedPlatforms', isMagazine ? itemList(item.featuredPlatforms).replace(/^\u2014$/, '') : '');
  setMaybeValue('editSpecialFeatures', isMagazine ? itemList(item.specialFeatures).replace(/^\u2014$/, '') : '');
  setMaybeValue('editIncludedExtras', (isMagazine || isStrategyGuide || isManual) ? itemList(item.includedExtras).replace(/^\u2014$/, '') : '');
  setMaybeValue('editCoveredGames', isStrategyGuide ? itemList(item.coveredGames).replace(/^\u2014$/, '') : '');
  setMaybeValue('editCoveredPlatforms', isStrategyGuide ? itemList(item.coveredPlatforms || item.associatedPlatforms).replace(/^\u2014$/, '') : '');
  setMaybeValue('editGuideTopics', isStrategyGuide ? itemList(item.guideTopics).replace(/^\u2014$/, '') : '');
  setMaybeValue('editStrategySpecialFeatures', isStrategyGuide ? itemList(item.specialFeatures).replace(/^\u2014$/, '') : '');
  setMaybeValue('editCharactersCovered', (isStrategyGuide || isManual) ? itemList(item.charactersCovered).replace(/^\u2014$/, '') : '');
  setMaybeValue('editLocationsCovered', isStrategyGuide ? itemList(item.locationsCovered).replace(/^\u2014$/, '') : '');
  setMaybeValue('editManualType', isManual ? (item.manualType || 'Instruction Manual') : '');
  setMaybeValue('editIncludedSections', isManual ? itemList(item.includedSections).replace(/^\u2014$/, '') : '');
  setMaybeValue('editControlScheme', isManual ? item.controlScheme : '');
  setMaybeValue('editItemsCovered', isManual ? itemList(item.itemsCovered).replace(/^\u2014$/, '') : '');
  setMaybeValue('editWarrantySupport', isManual ? item.warrantySupport : '');
  $('editTags').value = (item.tags || []).join(', ');
  $('notesText').value = item.notes || '';
  renderDetailReadingProfilePanel(item);
  updateMetadataExportButtonLabel(item.kind || '');
  ensureOpenLibraryMetadataUi();
  ensureIgdbMetadataUi();
  ensureEsrbMetadataUi();
  addMetadataFieldLockButtons();
  refreshMetadataLockButtons();
  updateMetadataSourceActionVisibility();
  updateMetadataFileMaintenance();
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
    const preferredPlatform = selectedKind === 'Magazine'
      ? ''
      : (selectedKind === 'Strategy Guide' && hasMultipleAssociatedPlatforms(associatedPlatforms)
        ? MULTI_PLATFORM_LABEL
        : ($('editCategory').value || ''));
    const magazineTitleValue = selectedKind === 'Magazine' ? ($('editMagazineTitle')?.value || $('editSeries').value || $('editTitle').value || '') : '';
    const magazinePayload = selectedKind === 'Magazine' ? {
      magazineTitle: magazineTitleValue,
      series: magazineTitleValue,
      volume: $('editVolume')?.value || '',
      coverDate: $('editCoverDate')?.value || '',
      barcodeUpcIssn: $('editBarcodeUpcIssn')?.value || '',
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
      manualTitle: $('editTitle')?.value || '',
      manualType: $('editManualType')?.value || 'Instruction Manual',
      gameTitle: $('editGameTitle')?.value || $('editSeries')?.value || $('editTitle')?.value || '',
      publicationDate: '',
      region: $('editRegion')?.value || '',
      franchise: $('editFranchise')?.value || $('editSeries')?.value || '',
      developer: $('editDeveloper')?.value || '',
      gamePublisher: $('editGamePublisher')?.value || '',
      gameReleaseYear: $('editGameReleaseYear')?.value || '',
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
      metadataStatus: normalizeMetadataStatus($('editMetadataStatus')?.value || state.selected?.metadataStatus || 'Unreviewed'),
      category: preferredPlatform,
      system: preferredPlatform,
      associatedPlatforms: selectedKind === 'Strategy Guide' ? associatedPlatforms : [],
      platformMatchTitle: selectedKind === 'Strategy Guide' ? ($('editPlatformMatchTitle')?.value || '') : '',
      series: $('editSeries').value,
      issueNumber: selectedKind === 'Magazine' ? $('editIssue').value : '',
      publisher: $('editPublisher').value,
      year: (selectedKind === 'Manual' || selectedKind === 'Strategy Guide') ? '' : $('editYear').value,
      pageCount: numericInput('editPageCount'),
      metadataPageCount: numericInput('editPageCount'),
      writer: $('editWriter').value,
      rating: selectedKind === 'Magazine' ? '' : ($('editEsrbRating')?.value || ''),
      summary: $('editSummary').value,
      tags,
      notes: $('notesText').value,
      metadataLocks: currentMetadataLocksPayload(),
      languageTag: ($('editLanguageTag')?.value || ''),
      ...magazinePayload,
      ...strategyPayload,
      ...manualPayload,
      ...extra
    };

    // Keep an exact client-side snapshot of what the user submitted. The server
    // response can still contain derived scan metadata, but it should never be
    // allowed to immediately overwrite the just-entered form values.
    const lockFiltered = filterLockedMetadataPayload(state.selected, payload);
    const submitted = normalizeClientMetadataPayload(lockFiltered.payload);
    if (lockFiltered.skipped.length) setStatus(`Skipped ${lockFiltered.skipped.length} locked metadata field(s).`);
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
        const matchesCustom = !state.customFilter || customSideNavItemMatches(item, state.customFilter);
        return matchesFilter && matchesCategory && matchesCustom && (!q || libraryItemSearchHaystack(item).includes(q));
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
      prepareLibraryItemComputedFields(updated);
      markLibraryIndexesDirty();
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
  state.reader.bookmarkMenuOpen = false;
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
  await requestReaderFullscreenFromProfile();
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

function setReaderImageSource(imgId, url = '') {
  const img = typeof imgId === 'string' ? $(imgId) : imgId;
  if (!img) return;
  const next = String(url || '');
  if (img.getAttribute('src') === next) return;
  img.decoding = 'async';
  img.loading = 'eager';
  img.src = next;
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
    setReaderImageSource('pageRightImage', spread.rightUrl);
    setReaderImageSource('pageLeftImage', '');
    if ($('pageLabel')) $('pageLabel').textContent = spread.label;
    updateReaderOverlay(spread);
    updateReaderPageStackEffect(spread);
    scheduleReaderPageEdgeShadingBounds();
    if (state.reader.magnifierActive) { updateReaderMagnifierContent(); requestAnimationFrame(updateReaderMagnifierFromLastPointer); }
    return;
  }

  if (spread.isAdaptiveSpread) {
    setReaderImageSource('pageLeftImage', '');
    setReaderImageSource('pageRightImage', spread.adaptiveUrl || spread.rightUrl || '');
    $('pageRight').classList.remove('blank-page');
    if ($('pageLabel')) $('pageLabel').textContent = spread.label;
    updateReaderOverlay(spread);
    updateReaderPageStackEffect(spread);
    scheduleReaderPageEdgeShadingBounds();
    if (state.reader.magnifierActive) { updateReaderMagnifierContent(); requestAnimationFrame(updateReaderMagnifierFromLastPointer); }
    return;
  }

  setReaderImageSource('pageLeftImage', spread.leftUrl);
  setReaderImageSource('pageRightImage', spread.rightUrl);
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

function normalizeReaderBookmarkRecord(value = null, item = state.reader.item || {}) {
  const pagesSource = Array.isArray(value?.pages)
    ? value.pages
    : (Number(value?.page) ? [{
        page: value.page,
        displayMode: value.displayMode,
        savedAt: value.savedAt
      }] : []);
  const seen = new Set();
  const pages = pagesSource
    .map(entry => ({
      page: Math.max(1, Math.min(Number(entry?.page) || 1, readerPageCount())),
      displayMode: normalizeReaderDisplayMode(entry?.displayMode ?? state.reader.displayMode),
      savedAt: entry?.savedAt || value?.savedAt || new Date().toISOString()
    }))
    .filter(entry => {
      const key = String(entry.page);
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    })
    .sort((a, b) => Number(a.page) - Number(b.page));
  return {
    itemId: value?.itemId || item?.id || '',
    title: value?.title || item?.title || '',
    pages,
    updatedAt: value?.updatedAt || value?.savedAt || (pages[pages.length - 1]?.savedAt || '')
  };
}

function getReaderBookmarkRecord() {
  const key = readerBookmarkKey();
  if (!key) return normalizeReaderBookmarkRecord();
  const bookmarks = loadReaderBookmarks();
  return normalizeReaderBookmarkRecord(bookmarks[key], state.reader.item || {});
}

function getReaderBookmark() {
  const page = currentReaderPageNumber();
  return getReaderBookmarkRecord().pages.find(entry => Number(entry.page) === Number(page)) || null;
}

function currentReaderPageNumber() {
  return Math.max(1, Math.min(readerSliderPageFromSpread(spreadForIndex(state.reader.index)), readerPageCount()));
}

function bookmarkCurrentReaderPage() {
  const key = readerBookmarkKey();
  if (!key || !state.reader.item) return;
  const page = currentReaderPageNumber();
  const bookmarks = loadReaderBookmarks();
  const record = normalizeReaderBookmarkRecord(bookmarks[key], state.reader.item);
  const existingIndex = record.pages.findIndex(entry => Number(entry.page) === Number(page));

  if (existingIndex >= 0) {
    record.pages.splice(existingIndex, 1);
  } else {
    record.pages.push({
      page,
      displayMode: normalizeReaderDisplayMode(state.reader.displayMode),
      savedAt: new Date().toISOString()
    });
  }

  record.pages.sort((a, b) => Number(a.page) - Number(b.page));
  record.updatedAt = new Date().toISOString();
  record.itemId = state.reader.item.id || record.itemId || '';
  record.title = state.reader.item.title || record.title || '';

  if (record.pages.length) bookmarks[key] = record;
  else delete bookmarks[key];

  saveReaderBookmarks(bookmarks);
  updateReaderOverlay();
}

function readerBookmarkPageDescription(page) {
  const numeric = Math.max(1, Math.min(Number(page) || 1, readerPageCount()));
  const spread = spreadForIndex(numeric - 1);
  return spread?.positionText || spread?.label || `Page ${numeric}`;
}

function setReaderBookmarkMenuVisible(visible) {
  state.reader.bookmarkMenuOpen = !!visible && !!state.reader.overlayVisible;
  updateReaderBookmarksUi();
}

function toggleReaderBookmarkMenu() {
  setReaderBookmarkMenuVisible(!state.reader.bookmarkMenuOpen);
}

function jumpReaderToBookmarkedPage(page) {
  setReaderBookmarkMenuVisible(false);
  jumpReaderToPage(page);
}

function updateReaderBookmarksUi() {
  const sliderPage = currentReaderPageNumber();
  const record = getReaderBookmarkRecord();
  const pages = record.pages || [];
  const bookmark = pages.find(entry => Number(entry.page) === Number(sliderPage));
  const bookmarkButton = $('readerBookmarkPage');
  if (bookmarkButton) {
    const samePage = !!bookmark;
    bookmarkButton.classList.toggle('bookmarked', samePage);
    bookmarkButton.setAttribute('aria-pressed', samePage ? 'true' : 'false');
    bookmarkButton.setAttribute('aria-label', samePage ? `Remove bookmark on Page ${bookmark.page}` : 'Bookmark current page');
    bookmarkButton.title = samePage ? `Remove bookmark on Page ${bookmark.page}` : 'Bookmark current page';
  }

  const toggle = $('readerBookmarksToggle');
  if (toggle) {
    toggle.disabled = !pages.length;
    toggle.classList.toggle('has-bookmarks', !!pages.length);
    toggle.setAttribute('aria-expanded', state.reader.bookmarkMenuOpen && pages.length ? 'true' : 'false');
    toggle.title = pages.length ? `${pages.length} bookmarked page${pages.length === 1 ? '' : 's'}` : 'No bookmarked pages yet';
    const count = $('readerBookmarkCount');
    if (count) count.textContent = pages.length ? String(pages.length) : '';
  }

  const panel = $('readerBookmarksPanel');
  const list = $('readerBookmarksList');
  if (!panel || !list) return;
  const open = !!state.reader.bookmarkMenuOpen && !!pages.length && !!state.reader.overlayVisible;
  panel.classList.toggle('hidden', !open);
  panel.classList.toggle('open', open);
  if (!open) return;
  list.innerHTML = pages.map(entry => {
    const page = Math.max(1, Math.min(Number(entry.page) || 1, readerPageCount()));
    const active = Number(page) === Number(sliderPage);
    const saved = entry.savedAt ? new Date(entry.savedAt) : null;
    const savedLabel = saved && !Number.isNaN(saved.getTime()) ? saved.toLocaleDateString([], { month: 'short', day: 'numeric' }) : '';
    return `<button class="reader-bookmark-item${active ? ' active' : ''}" type="button" data-bookmark-page="${page}"><strong>Page ${page}</strong><span>${escapeHtml(readerBookmarkPageDescription(page))}</span>${savedLabel ? `<em>${escapeHtml(savedLabel)}</em>` : ''}</button>`;
  }).join('');
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
  const normalized = normalizeReaderTransitionMode(mode);
  if (normalized === 'fade') return 'Quick Fade';
  if (normalized === 'slide') return 'Slide';
  if (normalized === 'page') return 'Page Turn Effect';
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

  updateReaderBookmarksUi();
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
    state.reader.bookmarkMenuOpen = false;
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

    // Stage 1: the part that tested well \u2014 a pure, smooth slide from the centered
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

  const preloadTargets = [toSpread.leftUrl, toSpread.rightUrl, toSpread.adaptiveUrl].filter(Boolean);
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
      <small>${status}${updated ? ` \u2022 ${escapeHtml(new Date(updated).toLocaleTimeString())}` : ''}</small>
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
  return list.map(([label, value], index) => `<div class="statistics-rank-row"><span>${index + 1}</span><strong>${escapeHtml(label || '\u2014')}</strong><em>${escapeHtml(String(value))} ${escapeHtml(unit)}</em></div>`).join('');
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
  if ($('statisticsRecentViewed')) $('statisticsRecentViewed').innerHTML = recentItems.length ? recentItems.map(item => `<div class="statistics-book-row"><img loading="lazy" src="${coverUrl(item)}" alt="" /><span><strong>${escapeHtml(displayTitle(item))}</strong><em>${escapeHtml(item.kind || '')} \u2022 ${escapeHtml(item.year || '\u2014')}</em></span></div>`).join('') : '<p class="sub">Open a few items to populate recent views.</p>';

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
  const chartNo = (lineChartSvg._counter = (lineChartSvg._counter || 0) + 1);
  const safeId = `gvStatsChart${chartNo}`;
  const w = 1600, h = 330, padLeft = 50, padRight = 14, padTop = 38, padBottom = 58;
  const plotW = w - padLeft - padRight;
  const plotH = h - padTop - padBottom;
  const baselineY = padTop + plotH;
  const hasData = keys.some(k => (seriesMap[k] || []).some(v => v > 0));
  if (!hasData) return `<div class="statistics-empty-chart">${escapeHtml(emptyText || 'No chart data yet.')}</div>`;
  const ticks = [0, .25, .5, .75, 1];
  const grid = ticks.map(t => {
    const y = padTop + plotH * (1 - t);
    const value = Math.round(max * t);
    return `<line x1="${padLeft}" y1="${y.toFixed(1)}" x2="${w-padRight}" y2="${y.toFixed(1)}" class="statistics-grid-line"/><text class="statistics-axis-label" x="${padLeft-10}" y="${(y+4).toFixed(1)}" text-anchor="end">${escapeHtml(String(value))}</text>`;
  }).join('');
  const xGrid = labels.map((label,i) => {
    const x = padLeft + plotW*(i/Math.max(1, labels.length-1));
    return `<line x1="${x.toFixed(1)}" y1="${padTop}" x2="${x.toFixed(1)}" y2="${baselineY}" class="statistics-grid-line vertical"/>`;
  }).join('');
  const defs = `<defs>
    <filter id="${safeId}Glow" x="-18%" y="-35%" width="136%" height="170%">
      <feDropShadow dx="0" dy="6" stdDeviation="5" flood-color="#000000" flood-opacity="0.34"/>
      <feDropShadow dx="0" dy="0" stdDeviation="3" flood-color="#76c8ff" flood-opacity="0.28"/>
    </filter>
    <linearGradient id="${safeId}Frame" x1="0" x2="0" y1="0" y2="1">
      <stop offset="0" stop-color="#15345d" stop-opacity="0.78"/>
      <stop offset="0.62" stop-color="#071423" stop-opacity="0.72"/>
      <stop offset="1" stop-color="#02070d" stop-opacity="0.88"/>
    </linearGradient>
    ${keys.map((key, idx) => `<linearGradient id="${safeId}Area${idx}" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stop-color="${colors[idx % colors.length]}" stop-opacity="0.38"/><stop offset="0.72" stop-color="${colors[idx % colors.length]}" stop-opacity="0.10"/><stop offset="1" stop-color="${colors[idx % colors.length]}" stop-opacity="0"/></linearGradient>`).join('')}
  </defs>`;
  const chartFrame = `<rect x="${(padLeft-12).toFixed(1)}" y="${(padTop-20).toFixed(1)}" width="${(plotW+24).toFixed(1)}" height="${(plotH+34).toFixed(1)}" rx="18" class="statistics-chart-frame" fill="url(#${safeId}Frame)"/>
    <path class="statistics-chart-floor" d="M${padLeft},${baselineY} L${w-padRight},${baselineY} L${w-padRight-22},${h-38} L${padLeft+22},${h-38} Z"/>`;
  const paths = keys.map((key, idx) => {
    const vals = seriesMap[key] || [];
    const points = vals.map((v,i) => {
      const x = padLeft + plotW * (i / Math.max(1, vals.length-1));
      const y = padTop + plotH * (1 - (v / max));
      return [x,y];
    });
    const d = points.map((p,i)=>`${i?'L':'M'}${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' ');
    const areaD = `${d} L${points[points.length-1][0].toFixed(1)},${baselineY.toFixed(1)} L${points[0][0].toFixed(1)},${baselineY.toFixed(1)} Z`;
    const offsetD = points.map((p,i)=>`${i?'L':'M'}${p[0].toFixed(1)},${(p[1]+8).toFixed(1)}`).join(' ');
    const color = colors[idx % colors.length];
    const circles = points.map(p=>`<circle class="statistics-point-halo" cx="${p[0].toFixed(1)}" cy="${p[1].toFixed(1)}" r="8" fill="${color}"/><circle class="statistics-point" cx="${p[0].toFixed(1)}" cy="${p[1].toFixed(1)}" r="3.8" fill="${color}"/>`).join('');
    return `<path class="statistics-series-area" d="${areaD}" fill="url(#${safeId}Area${idx})"/><path class="statistics-series-depth" d="${offsetD}"/><path class="statistics-series-line" d="${d}" fill="none" stroke="${color}" filter="url(#${safeId}Glow)"/>${circles}`;
  }).join('');
  const xLabels = labels.map((label,i) => { const x = padLeft + plotW*(i/Math.max(1, labels.length-1)); return `<text class="statistics-axis-label" x="${x.toFixed(1)}" y="${h-20}" text-anchor="middle">${escapeHtml(statisticsMonthLabel(label, i))}</text>`; }).join('');
  const yCaption = options.yLabel ? `<text class="statistics-axis-caption" x="16" y="${padTop + plotH/2}" text-anchor="middle" transform="rotate(-90 16 ${padTop + plotH/2})">${escapeHtml(options.yLabel)}</text>` : '';
  const xCaption = options.xLabel ? `<text class="statistics-axis-caption" x="${padLeft + plotW/2}" y="${h-4}" text-anchor="middle">${escapeHtml(options.xLabel)}</text>` : '';
  return `<svg class="statistics-chart-svg enhanced" viewBox="0 0 ${w} ${h}" role="img" aria-label="Statistics line chart">${defs}${chartFrame}${xGrid}${grid}${paths}${xLabels}${yCaption}${xCaption}</svg>`;
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

const GUIDEVAULT_SETTINGS_GROUPS = {
  account: ['account', 'preferences', 'keybinds', 'reading-profiles', 'customize', 'devices'],
  insights: ['insights', 'insights-devices', 'statistics'],
  server: ['server', 'files', 'integrations', 'metadata-manager', 'opds', 'media', 'email', 'users', 'tasks'],
  info: ['info', 'events']
};

function settingsGroupForTab(tab = 'account') {
  const normalized = tab === 'insights' ? 'statistics' : tab;
  return Object.entries(GUIDEVAULT_SETTINGS_GROUPS).find(([, tabs]) => tabs.includes(normalized))?.[0] || 'account';
}

function loadSettingsNavCollapsed() {
  if (state.settingsNavCollapsed && Object.keys(state.settingsNavCollapsed).length) return state.settingsNavCollapsed;
  const collapsed = {};
  try {
    const raw = localStorage.getItem(GUIDEVAULT_SETTINGS_NAV_KEY);
    const saved = raw ? JSON.parse(raw) : {};
    Object.keys(GUIDEVAULT_SETTINGS_GROUPS).forEach(group => { collapsed[group] = !!saved?.[group]; });
  } catch {
    Object.keys(GUIDEVAULT_SETTINGS_GROUPS).forEach(group => { collapsed[group] = false; });
  }
  state.settingsNavCollapsed = collapsed;
  return collapsed;
}

function saveSettingsNavCollapsed() {
  try { localStorage.setItem(GUIDEVAULT_SETTINGS_NAV_KEY, JSON.stringify(state.settingsNavCollapsed || {})); } catch {}
}

function setSettingsGroupCollapsed(group, collapsed, persist = true) {
  if (!GUIDEVAULT_SETTINGS_GROUPS[group]) return;
  loadSettingsNavCollapsed();
  state.settingsNavCollapsed[group] = !!collapsed;
  if (persist) saveSettingsNavCollapsed();
  updateSettingsNavGroups(state.settingsActiveTab || 'account');
}

function updateSettingsNavGroups(active = state.settingsActiveTab || 'account') {
  const collapsed = loadSettingsNavCollapsed();
  const activeGroup = settingsGroupForTab(active);
  document.querySelectorAll('.settings-nav-group').forEach(btn => {
    const group = btn.dataset.settingsGroup || settingsGroupForTab(btn.dataset.settingsTab || 'account');
    const isCollapsed = !!collapsed[group];
    btn.classList.toggle('collapsed', isCollapsed);
    btn.classList.toggle('expanded', !isCollapsed);
    btn.classList.toggle('group-active', group === activeGroup);
    btn.setAttribute('aria-expanded', String(!isCollapsed));
  });
  document.querySelectorAll('.settings-submenu[data-settings-parent]').forEach(menu => {
    const group = menu.dataset.settingsParent || '';
    const isCollapsed = !!collapsed[group];
    menu.classList.toggle('collapsed', isCollapsed);
    menu.classList.toggle('hidden', isCollapsed);
  });
}

function handleSettingsNavClick(btn) {
  const tab = btn.dataset.settingsTab || 'account';
  const group = btn.dataset.settingsGroup || settingsGroupForTab(tab);
  loadSettingsNavCollapsed();
  if (btn.classList.contains('settings-subnav')) {
    state.settingsNavCollapsed[group] = false;
    saveSettingsNavCollapsed();
  } else if (btn.classList.contains('settings-nav-group')) {
    const activeGroup = settingsGroupForTab(state.settingsActiveTab || 'account');
    const alreadyActiveGroup = activeGroup === group;
    const currentlyCollapsed = !!state.settingsNavCollapsed[group];
    if (alreadyActiveGroup && (state.settingsActiveTab === tab || btn.classList.contains('active'))) {
      state.settingsNavCollapsed[group] = !currentlyCollapsed;
    } else {
      state.settingsNavCollapsed[group] = false;
    }
    saveSettingsNavCollapsed();
  }
  activateSettingsTab(tab);
}


function resetGuidevaultLandingToHome(options = {}) {
  const shouldRender = options.render !== false;
  state.filter = 'All Content';
  state.categoryFilter = '';
  state.customFilter = null;
  state.viewMode = 'all';
  state.selected = null;
  if ($('search')) $('search').value = '';
  setDefaultSortForCurrentLibraryView();
  document.querySelectorAll('.chip').forEach(c => c.classList.remove('active'));
  try {
    const clean = `${window.location.pathname}${window.location.search}` || '/';
    if (window.location.hash) history.replaceState(null, '', clean);
  } catch {}
  showLibraryScreen();
  updateNavActive();
  scrollMainToTop();
  if (shouldRender) applyFilters();
}

function navigateGuidevaultHome() {
  resetGuidevaultLandingToHome({ render: true });
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
  if (tab === 'email-history') tab = 'email';
  const allowed = new Set(['account', 'preferences', 'keybinds', 'reading-profiles', 'customize', 'opds', 'devices', 'insights-devices', 'statistics', 'server', 'files', 'integrations', 'metadata-manager', 'media', 'email', 'users', 'tasks', 'info', 'events']);
  const active = allowed.has(tab) ? tab : 'account';
  state.settingsActiveTab = active;
  const activeGroup = settingsGroupForTab(active);
  document.querySelectorAll('.settings-nav, .settings-subnav').forEach(btn => {
    const navTab = btn.dataset.settingsTab || '';
    const navGroup = btn.dataset.settingsGroup || settingsGroupForTab(navTab);
    const isParentForActiveGroup = btn.classList.contains('settings-nav') && navGroup === activeGroup;
    btn.classList.toggle('active', navTab === active || isParentForActiveGroup);
  });
  updateSettingsNavGroups(active);
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
  if ($('settingsFilesPanel')) $('settingsFilesPanel').classList.toggle('hidden', active !== 'files');
  if ($('settingsIntegrationsPanel')) $('settingsIntegrationsPanel').classList.toggle('hidden', active !== 'integrations');
  if ($('settingsMediaPanel')) $('settingsMediaPanel').classList.toggle('hidden', active !== 'media');
  if ($('settingsEmailPanel')) $('settingsEmailPanel').classList.toggle('hidden', active !== 'email');
  if ($('settingsUsersPanel')) $('settingsUsersPanel').classList.toggle('hidden', active !== 'users');
  if ($('settingsTasksPanel')) $('settingsTasksPanel').classList.toggle('hidden', active !== 'tasks');
  if ($('settingsMetadataManagerPanel')) $('settingsMetadataManagerPanel').classList.toggle('hidden', active !== 'metadata-manager');
  if ($('settingsImportPanel')) $('settingsImportPanel').classList.toggle('hidden', active !== 'media');
  if ($('settingsInfoPanel')) $('settingsInfoPanel').classList.toggle('hidden', active !== 'info');
  if ($('settingsEventsPanel')) $('settingsEventsPanel').classList.toggle('hidden', active !== 'events');
  if (active === 'preferences') renderPreferencesSettings();
  if (active === 'keybinds') renderKeybindsSettings();
  if (active === 'customize') renderCustomizeSettings();
  if (active === 'server') loadServerSettings(false);
  if (active === 'files') renderServerFilesWorkspace();
  if (active === 'integrations') loadServerSettings(false);
  if (active === 'media') loadServerSettings(false);
  if (active === 'email') { if (!state.serverSettings) loadServerSettings(false); loadEmailSettings(false); loadEmailHistory(false); requestAnimationFrame(syncEmailTemplatePreview); }
  if (active === 'users') { renderUsersLoadingState(); deferAfterVisiblePaint(() => openUsersSettingsPanel(), 120); }
  if (active === 'tasks') loadTaskSettings(false);
  if (active === 'statistics') renderStatistics();
  if (active === 'info') { trimSystemUpdateHistory(); loadSystemInfo(false); loadSystemPerformance(); checkStableUpdates(false); }
  if (active === 'events') loadSystemEvents(false);
  if (active === 'reading-profiles') renderReadingProfileSettings();
  if (active === 'opds') { renderOpdsSettings(); syncOpdsSettingsFromServer(false); }
  if (active === 'devices' || active === 'insights-devices') { renderDeviceHistory(); sendDeviceHeartbeat({ refresh: true }); loadDeviceHistory(false); }
  if (active === 'metadata-manager') renderMetadataManager();
  if ($('settingsImportTitle')) $('settingsImportTitle').textContent = 'Libraries';
  if ($('settingsImportSub')) $('settingsImportSub').textContent = 'Save folder paths, rescan sources, and manage scan-in-place library entries. Source files stay where they already are.';
}




function bindGuidevaultTopbarAction(id, handler) {
  const el = $(id);
  if (!el || el.dataset.guidevaultTopbarBound === '1') return;
  el.dataset.guidevaultTopbarBound = '1';
  el.addEventListener('click', handler);
}

function bindGuidevaultTopbarActions() {
  bindGuidevaultTopbarAction('userMenuBtn', e => { e.preventDefault(); e.stopPropagation(); toggleUserMenu(); });
  bindGuidevaultTopbarAction('userMenuProfile', e => { e.preventDefault(); e.stopPropagation(); setUserMenuOpen(false); showUserProfilePage(); });
  bindGuidevaultTopbarAction('userMenuHelp', e => { e.preventDefault(); setUserMenuOpen(false); showSettingsScreen('keybinds'); });
  bindGuidevaultTopbarAction('userMenuLogout', e => { e.preventDefault(); setUserMenuOpen(false); logoutGuidevault(); });
  bindGuidevaultTopbarAction('settingsBtn', e => { e.preventDefault(); e.stopPropagation(); showSettingsScreen('account'); });
  bindGuidevaultTopbarAction('taskMonitorBtn', e => { e.preventDefault(); e.stopPropagation(); setTaskPanelVisible(!state.taskPanelVisible); pollTasks(false); });
}

function getGuidevaultTopbarHitTarget(event) {
  const topbar = document.querySelector('.topbar');
  if (!topbar || topbar.contains(event.target)) return null;
  const point = event.touches?.[0] || event.changedTouches?.[0] || event;
  const x = point.clientX;
  const y = point.clientY;
  if (!Number.isFinite(x) || !Number.isFinite(y)) return null;
  const topbarRect = topbar.getBoundingClientRect();
  if (x < topbarRect.left || x > topbarRect.right || y < topbarRect.top || y > topbarRect.bottom) return null;
  const hitTargets = ['taskMonitorBtn', 'settingsBtn', 'userMenuBtn'];
  return hitTargets.map(id => $(id)).find(el => {
    if (!el) return false;
    const rect = el.getBoundingClientRect();
    return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;
  }) || null;
}

function ensureTopbarInputIsInteractive() {
  const topbar = document.querySelector('.topbar');
  if (!topbar) return;
  bindGuidevaultTopbarActions();
  if (topbar.dataset.guidevaultTopbarGuard === '1') return;
  topbar.dataset.guidevaultTopbarGuard = '1';
  ['pointerdown', 'mousedown', 'touchstart'].forEach(eventName => {
    topbar.addEventListener(eventName, () => {
      document.body.classList.remove('panel-animating');
    }, true);
  });
  ['pointerdown', 'mousedown', 'touchstart', 'click'].forEach(eventName => {
    document.addEventListener(eventName, event => {
      const target = getGuidevaultTopbarHitTarget(event);
      if (!target) return;
      document.body.classList.remove('panel-animating');
      if (eventName === 'click') {
        event.preventDefault();
        event.stopPropagation();
        target.click();
      }
    }, true);
  });
}

setupHomebarIconFallbacks();
ensureTopbarInputIsInteractive();
try { loadCategoryStructure(); } catch (err) { console.warn('Guidevault category structure failed to load.', err); }
try { loadLibraryCoverScale(); } catch (err) { console.warn('Guidevault cover scale failed to load.', err); }
if ($('leftToggle')) $('leftToggle').addEventListener('click', () => runPanelTransition(() => document.body.classList.toggle('left-collapsed')));
if ($('rightToggleTop')) $('rightToggleTop').addEventListener('click', () => toggleRightPanel());
if ($('rightToggle')) $('rightToggle').addEventListener('click', () => toggleRightPanel(false));
if ($('search')) $('search').addEventListener('input', () => {
  window.clearTimeout(guidevaultLibrarySearchTimer);
  guidevaultLibrarySearchTimer = window.setTimeout(applyFilters, GUIDEVAULT_LIBRARY_SEARCH_DEBOUNCE_MS);
});
if ($('sort')) $('sort').addEventListener('change', applyFilters);
if ($('coverSizeSlider')) $('coverSizeSlider').addEventListener('input', e => setLibraryCoverScale(e.currentTarget.value));
if ($('categoryStructureSelect')) $('categoryStructureSelect').addEventListener('change', e => { saveCategoryStructure(e.currentTarget.value); state.categoryFilter = ''; state.customFilter = null; state.filter = 'All Content'; state.viewMode = 'all'; setDefaultSortForCurrentLibraryView(); updateNavActive(); applyFilters(); });
document.querySelectorAll('.nav').forEach(btn => btn.addEventListener('click', () => {
  showLibraryScreen();
  state.viewMode = btn.dataset.view || 'all';
  state.filter = btn.dataset.filter || 'All Content';
  state.categoryFilter = '';
  state.customFilter = null;
  if ($('search')) $('search').value = '';
  setDefaultSortForCurrentLibraryView();
  document.querySelectorAll('.chip').forEach(c => c.classList.toggle('active', c.dataset.kind === state.filter || (state.filter === 'All Content' && c.dataset.kind === 'All Content')));
  updateNavActive();
  scrollMainToTop();
  applyFilters();
}));
document.querySelectorAll('.chip').forEach(btn => btn.addEventListener('click', () => {
  showLibraryScreen();
  state.filter = btn.dataset.kind;
  state.viewMode = state.filter === 'All Content' ? 'all' : state.filter === 'Manual' ? 'manuals' : state.filter === 'Strategy Guide' ? 'strategy-guides' : 'magazine-series';
  state.categoryFilter = '';
  state.customFilter = null;
  setDefaultSortForCurrentLibraryView();
  document.querySelectorAll('.chip').forEach(c => c.classList.toggle('active', c === btn));
  updateNavActive();
  scrollMainToTop();
  applyFilters();
}));
document.querySelectorAll('.collection').forEach(btn => {
  const prewarm = () => {
    const kind = btn.dataset.kind || 'All Content';
    if (['Manual', 'Strategy Guide', 'Magazine'].includes(kind)) {
      scheduleCategoryPreviewCoverPrewarm(kind, { immediate: true, includeSecondary: true });
    }
  };
  btn.addEventListener('pointerenter', prewarm);
  btn.addEventListener('focus', prewarm);
  btn.addEventListener('click', () => {
    showLibraryScreen();
    const kind = btn.dataset.kind || 'All Content';
    state.filter = kind;
    state.viewMode = kind === 'Manual' ? 'manual-systems' : kind === 'Strategy Guide' ? 'guide-systems' : kind === 'Magazine' ? 'magazine-series' : 'all';
    state.categoryFilter = '';
    state.customFilter = null;
    if ($('search')) $('search').value = '';
    setDefaultSortForCurrentLibraryView();
    document.querySelectorAll('.chip').forEach(c => c.classList.toggle('active', c.dataset.kind === kind));
    updateNavActive();
    scrollMainToTop();
    prewarm();
    applyFilters();
  });
});
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
if ($('readerBookmarksToggle')) $('readerBookmarksToggle').addEventListener('click', e => { e.preventDefault(); e.stopPropagation(); setReaderOverlayVisible(true); toggleReaderBookmarkMenu(); });
if ($('readerBookmarksList')) $('readerBookmarksList').addEventListener('click', e => {
  const button = e.target.closest?.('[data-bookmark-page]');
  if (!button) return;
  e.preventDefault();
  jumpReaderToBookmarkedPage(button.dataset.bookmarkPage);
});
document.addEventListener('click', e => {
  if (!state.reader?.bookmarkMenuOpen) return;
  if (e.target.closest?.('.reader-bookmark-wrap')) return;
  setReaderBookmarkMenuVisible(false);
}, true);
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
if ($('userMenuBtn') && $('userMenuBtn').dataset.guidevaultTopbarBound !== '1') $('userMenuBtn').addEventListener('click', e => { e.preventDefault(); e.stopPropagation(); toggleUserMenu(); });
if ($('userMenuProfile') && $('userMenuProfile').dataset.guidevaultTopbarBound !== '1') $('userMenuProfile').addEventListener('click', e => { e.preventDefault(); e.stopPropagation(); setUserMenuOpen(false); showUserProfilePage(); });
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
if ($('userMenuHelp') && $('userMenuHelp').dataset.guidevaultTopbarBound !== '1') $('userMenuHelp').addEventListener('click', e => { e.preventDefault(); setUserMenuOpen(false); showSettingsScreen('keybinds'); });
if ($('userMenuLogout') && $('userMenuLogout').dataset.guidevaultTopbarBound !== '1') $('userMenuLogout').addEventListener('click', e => { e.preventDefault(); setUserMenuOpen(false); logoutGuidevault(); });
document.addEventListener('click', e => { if (!$('userMenuPanel')?.classList.contains('hidden') && !e.target.closest?.('.top-user-wrap')) setUserMenuOpen(false); });
if ($('settingsBtn') && $('settingsBtn').dataset.guidevaultTopbarBound !== '1') $('settingsBtn').addEventListener('click', () => showSettingsScreen('account'));
if ($('settingsBackToLibrary')) $('settingsBackToLibrary').addEventListener('click', () => showLibraryScreen());
document.addEventListener('keydown', handleReaderKeydown, true);
if ($('loginForm')) $('loginForm').addEventListener('submit', handleLoginSubmit);
if ($('accountEditLogin')) $('accountEditLogin').addEventListener('click', e => { e.preventDefault(); setAccountEditMode(true); });
if ($('accountSaveLogin')) $('accountSaveLogin').addEventListener('click', e => { e.preventDefault(); saveAccountLoginFromSettings(); });
if ($('accountCancelEdit')) $('accountCancelEdit').addEventListener('click', e => { e.preventDefault(); cancelAccountEdit(); });
if ($('accountLogout')) $('accountLogout').addEventListener('click', e => { e.preventDefault(); logoutGuidevault(); });
if ($('preferenceUseColorscape')) $('preferenceUseColorscape').addEventListener('change', e => { setUseColorscapePreference(e.currentTarget.checked); });
[
  ['preferenceColorscapeDetailPane', 'colorscapeDetailPane'],
  ['preferenceColorscapeManualMenus', 'colorscapeManualMenus'],
  ['preferenceColorscapeStrategyMenus', 'colorscapeStrategyMenus'],
  ['preferenceColorscapeMagazineMenus', 'colorscapeMagazineMenus']
].forEach(([id, key]) => {
  if ($(id)) $(id).addEventListener('change', e => setGuidevaultPreferenceValue(key, e.currentTarget.checked));
});
if ($('keybindsList')) $('keybindsList').addEventListener('click', handleKeybindAction);
if ($('keybindsResetAll')) $('keybindsResetAll').addEventListener('click', e => { e.preventDefault(); resetAllKeybinds(); });
if ($('customizeAddShelf')) $('customizeAddShelf').addEventListener('click', e => { e.preventDefault(); addCustomizeShelf(); });
if ($('customizeShelfList')) {
  $('customizeShelfList').addEventListener('click', handleCustomizeShelfAction);
  $('customizeShelfList').addEventListener('dragstart', handleCustomizeShelfDragStart);
  $('customizeShelfList').addEventListener('dragover', handleCustomizeShelfDragOver);
  $('customizeShelfList').addEventListener('drop', handleCustomizeShelfDrop);
  $('customizeShelfList').addEventListener('dragend', handleCustomizeShelfDragEnd);
}
document.querySelectorAll('.customize-tab').forEach(btn => btn.addEventListener('click', handleCustomizeTabClick));
if ($('statisticsRange')) $('statisticsRange').addEventListener('change', e => { state.statistics.range = e.target.value || 'all'; renderStatistics(); });
document.querySelectorAll('.statistics-tab').forEach(btn => btn.addEventListener('click', () => setStatisticsTab(btn.dataset.statisticsTab || 'stats')));
if ($('statisticsRefresh')) $('statisticsRefresh').addEventListener('click', e => { e.preventDefault(); renderStatistics(); });
if ($('profileRange')) $('profileRange').addEventListener('change', e => { state.profilePage.range = e.target.value || 'all'; renderPersonalProfile(); });
document.querySelectorAll('.profile-tab').forEach(btn => btn.addEventListener('click', () => setProfileTab(btn.dataset.profileTab || 'overview')));
document.addEventListener('change', e => { if (e.target?.id === 'profileReviewItemSelect') syncProfileReviewEditorFromSelection(); });
document.addEventListener('click', e => {
  if (e.target?.closest?.('#profileSaveReview')) { e.preventDefault(); saveProfileReviewFromForm(); return; }
  if (e.target?.closest?.('#profileClearReview')) { e.preventDefault(); clearProfileReviewForm(); return; }
  const deleteBtn = e.target?.closest?.('[data-profile-review-delete]');
  if (deleteBtn) { e.preventDefault(); deleteProfileReview(deleteBtn.dataset.profileReviewDelete || ''); }
});
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
if ($('readingProfilePresetZoom')) $('readingProfilePresetZoom').addEventListener('input', refreshReadingProfilePresetZoomOutput); if ($('readingProfilePresetBrightness')) $('readingProfilePresetBrightness').addEventListener('input', refreshReadingProfilePresetBrightnessOutput);
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
if ($('taskMonitorBtn') && $('taskMonitorBtn').dataset.guidevaultTopbarBound !== '1') $('taskMonitorBtn').addEventListener('click', e => { e.preventDefault(); setTaskPanelVisible(!state.taskPanelVisible); pollTasks(false); });
if ($('updateNotifyBtn')) $('updateNotifyBtn').addEventListener('click', e => { e.preventDefault(); showSettingsScreen('info'); setSystemInfoStatus('A stable container image update is available. Pull the new image from your Docker host when ready.', 'success'); });
if ($('systemCheckUpdates')) $('systemCheckUpdates').addEventListener('click', async e => { e.preventDefault(); setSystemInfoStatus('Checking stable update feed...', 'info'); if ('Notification' in window && Notification.permission === 'default') { try { await Notification.requestPermission(); } catch {} } await checkStableUpdates(true); setSystemInfoStatus(state.updateCheck?.message || 'Update check complete.', state.updateCheck?.updateAvailable ? 'success' : ''); });
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
addMetadataFieldInfoIcons();
addMetadataFieldLockButtons();
document.addEventListener('click', e => {
  const lockButton = e.target.closest?.('.metadata-lock-button');
  if (lockButton) {
    e.preventDefault();
    toggleSelectedMetadataLock(lockButton.dataset.metadataLockKey || '');
    return;
  }
  const infoButton = e.target.closest?.('.metadata-info-button');
  if (infoButton) {
    e.preventDefault();
    const fieldId = infoButton.dataset.metadataHelpFor || '';
    const popover = document.querySelector(`.metadata-info-popover[data-metadata-help-text-for="${CSS.escape(fieldId)}"]`);
    if (popover) {
      const willOpen = popover.classList.contains('hidden');
      closeMetadataFieldInfoPopovers(willOpen ? popover : null);
      popover.classList.toggle('hidden', !willOpen);
      infoButton.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
    }
    return;
  }
  if (!e.target.closest?.('.metadata-info-button') && !e.target.closest?.('.metadata-info-popover')) closeMetadataFieldInfoPopovers();
  const button = e.target.closest?.('.meta-multi-button');
  if (button) {
    e.preventDefault();
    const shell = button.closest('.meta-multi-select');
    if (button.disabled || shell?.dataset?.metadataLocked === 'true') {
      closeMetadataMultiSelects();
      return;
    }
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
  if (input.disabled || shell?.dataset?.metadataLocked === 'true') {
    syncMetadataMultiSelectControl(id);
    return;
  }
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
if ($('saveMetadataBtn')) $('saveMetadataBtn').addEventListener('click', async e => { e.preventDefault(); await saveSelectedMetadata({}, { tab: state.activeTab || 'metadata', button: e.currentTarget }); });
if ($('lockAllMetadataFieldsBtn')) $('lockAllMetadataFieldsBtn').addEventListener('click', async e => { e.preventDefault(); await setAllSelectedMetadataFieldLocks(true); });
if ($('unlockAllMetadataFieldsBtn')) $('unlockAllMetadataFieldsBtn').addEventListener('click', async e => { e.preventDefault(); await setAllSelectedMetadataFieldLocks(false); });
if ($('exportGuideMetadataBtn')) $('exportGuideMetadataBtn').addEventListener('click', e => { e.preventDefault(); exportSelectedGuidevaultMetadata(); });
if ($('enrichCurrentFileMetadataBtn')) $('enrichCurrentFileMetadataBtn').addEventListener('click', e => { e.preventDefault(); enrichSelectedFileMetadata(); });
if ($('resetFileRenameSchemaBtn')) $('resetFileRenameSchemaBtn').addEventListener('click', e => { e.preventDefault(); resetFileRenameSchema(); });
if ($('fileRenameSchema')) {
  $('fileRenameSchema').addEventListener('input', () => updateMetadataFileMaintenance());
  $('fileRenameSchema').addEventListener('change', e => { saveFileRenameSchema(e.target.value); updateMetadataFileMaintenance(); });
}
document.addEventListener('click', e => {
  const tokenButton = e.target.closest?.('.filename-token-button');
  if (tokenButton) { e.preventDefault(); insertFilenameSchemaToken(tokenButton.dataset.token || ''); return; }
  const rename = e.target.closest?.('#renameToSuggestedFileNameBtn');
  if (rename) { e.preventDefault(); renameSelectedFileToSuggestedName(); return; }
  const reset = e.target.closest?.('#resetFileRenameSchemaBtn');
  if (reset) { e.preventDefault(); resetFileRenameSchema(); }
});
if ($('metadataPanel')) {
  $('metadataPanel').addEventListener('input', () => updateMetadataFileMaintenance());
  $('metadataPanel').addEventListener('change', () => updateMetadataFileMaintenance());
}
if ($('library-dataPanel')) {
  $('library-dataPanel').addEventListener('input', () => updateMetadataFileMaintenance());
  $('library-dataPanel').addEventListener('change', () => updateMetadataFileMaintenance());
}

document.addEventListener('click', e => {
  const openLibrary = e.target.closest?.('#openLibrarySearchBtn');
  if (openLibrary) {
    e.preventDefault();
    if (!metadataSourceLookupAllowedForSelectedItem('openLibrary')) { setStatus(metadataSourceLookupUnavailableMessage('Open Library metadata lookup')); return; }
    openOpenLibraryMetadataDialog();
    return;
  }
  const close = e.target.closest?.('#openLibraryCloseBtn, #openLibraryCancelBtn');
  if (close) { e.preventDefault(); closeOpenLibraryDialog(); return; }
  const run = e.target.closest?.('#openLibraryRunSearchBtn');
  if (run) { e.preventDefault(); runOpenLibraryMetadataSearch(false); return; }
  const select = e.target.closest?.('.openlibrary-select-result');
  if (select) { e.preventDefault(); selectOpenLibrarySearchResult(select.dataset.resultIndex || '0'); return; }
  const back = e.target.closest?.('#openLibraryBackToResultsBtn');
  if (back) { e.preventDefault(); renderOpenLibraryDialog('search'); return; }
  const selected = e.target.closest?.('#openLibraryImportSelectedBtn');
  if (selected) { e.preventDefault(); importOpenLibraryMetadata('selected'); return; }
  const empty = e.target.closest?.('#openLibraryImportEmptyBtn');
  if (empty) { e.preventDefault(); importOpenLibraryMetadata('empty'); return; }
  const all = e.target.closest?.('#openLibraryImportAllBtn');
  if (all) { e.preventDefault(); importOpenLibraryMetadata('all'); }
});

document.addEventListener('click', e => {
  const esrb = e.target.closest?.('#esrbSearchBtn');
  if (esrb) {
    e.preventDefault();
    if (!metadataSourceLookupAllowedForSelectedItem('esrb')) { setStatus(metadataSourceLookupUnavailableMessage('ESRB rating lookup')); return; }
    openEsrbMetadataDialog();
    return;
  }
  const close = e.target.closest?.('#esrbCloseBtn, #esrbCancelBtn');
  if (close) { e.preventDefault(); closeEsrbDialog(); return; }
  const run = e.target.closest?.('#esrbRunSearchBtn');
  if (run) { e.preventDefault(); runEsrbMetadataSearch(false); return; }
  const select = e.target.closest?.('.esrb-select-result');
  if (select) { e.preventDefault(); selectEsrbSearchResult(select.dataset.resultIndex || '0'); return; }
  const back = e.target.closest?.('#esrbBackToResultsBtn');
  if (back) { e.preventDefault(); renderEsrbDialog('search'); return; }
  const selected = e.target.closest?.('#esrbImportSelectedBtn');
  if (selected) { e.preventDefault(); importEsrbMetadata('selected'); return; }
  const empty = e.target.closest?.('#esrbImportEmptyBtn');
  if (empty) { e.preventDefault(); importEsrbMetadata('empty'); return; }
  const all = e.target.closest?.('#esrbImportAllBtn');
  if (all) { e.preventDefault(); importEsrbMetadata('all'); }
});
document.addEventListener('click', e => {
  const igdb = e.target.closest?.('#igdbSearchBtn');
  if (igdb) {
    e.preventDefault();
    if (!metadataSourceLookupAllowedForSelectedItem('igdb')) { setStatus(metadataSourceLookupUnavailableMessage('IGDB game metadata lookup')); return; }
    openIgdbMetadataDialog();
    return;
  }
  const close = e.target.closest?.('#igdbCloseBtn, #igdbCancelBtn');
  if (close) { e.preventDefault(); closeIgdbDialog(); return; }
  const run = e.target.closest?.('#igdbRunSearchBtn');
  if (run) { e.preventDefault(); runIgdbMetadataSearch(false); return; }
  const select = e.target.closest?.('.igdb-select-result');
  if (select) { e.preventDefault(); selectIgdbSearchResult(select.dataset.resultIndex || '0'); return; }
  const back = e.target.closest?.('#igdbBackToResultsBtn');
  if (back) { e.preventDefault(); renderIgdbDialog('search'); return; }
  const selected = e.target.closest?.('#igdbImportSelectedBtn');
  if (selected) { e.preventDefault(); importIgdbMetadata('selected'); return; }
  const empty = e.target.closest?.('#igdbImportEmptyBtn');
  if (empty) { e.preventDefault(); importIgdbMetadata('empty'); return; }
  const all = e.target.closest?.('#igdbImportAllBtn');
  if (all) { e.preventDefault(); importIgdbMetadata('all'); }
});
if ($('saveNotesBtn')) $('saveNotesBtn').addEventListener('click', async e => { e.preventDefault(); await saveSelectedMetadata({ notes: $('notesText').value }, { tab: 'notes', button: e.currentTarget }); });
if (document.querySelector('.brand-wordmark')) document.querySelector('.brand-wordmark').addEventListener('click', e => { e.preventDefault(); navigateGuidevaultHome(); });
document.querySelector('.brand-wordmark')?.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); navigateGuidevaultHome(); } });
document.querySelector('.brand-wordmark')?.addEventListener('click', e => { e.preventDefault(); navigateGuidevaultHome(); });
document.querySelector('.brand-wordmark')?.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); navigateGuidevaultHome(); } });
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
  handleSettingsNavClick(btn);
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


function metadataManagerCurrentSelectedIds() {
  const selected = Array.from(new Set((state.metadataManager?.selectedIds || []).map(id => String(id || '').trim()).filter(Boolean)));
  if (selected.length) return selected;
  const selectedItemId = metadataManagerItemId(state.selected || {});
  return selectedItemId ? [selectedItemId] : [];
}

function metadataManagerSelectedItemsFromState() {
  const ids = new Set(metadataManagerCurrentSelectedIds());
  return (state.items || []).filter(item => ids.has(metadataManagerItemId(item)));
}

function serverFilesEnsureState() {
  state.serverFiles = state.serverFiles || { selectedIds: [], kindFilters: ['Manual','Strategy Guide','Magazine'], search: '', renderLimit: METADATA_MANAGER_DEFAULT_RENDER_LIMIT, templateKind: 'manual', templateTargetId: 'serverFilesManualTemplate' };
  if (!Array.isArray(state.serverFiles.selectedIds)) state.serverFiles.selectedIds = [];
  if (!Array.isArray(state.serverFiles.kindFilters) || !state.serverFiles.kindFilters.length) state.serverFiles.kindFilters = ['Manual','Strategy Guide','Magazine'];
  if (!Number.isFinite(Number(state.serverFiles.renderLimit)) || Number(state.serverFiles.renderLimit) <= 0) state.serverFiles.renderLimit = METADATA_MANAGER_DEFAULT_RENDER_LIMIT;
  return state.serverFiles;
}

function serverFilesSelectedKinds() {
  const files = serverFilesEnsureState();
  const kinds = (files.kindFilters || []).filter(kind => METADATA_MANAGER_KIND_FILTERS.includes(kind));
  return kinds.length ? kinds : METADATA_MANAGER_KIND_FILTERS.slice();
}

function serverFilesKindSummaryLabel(kinds = serverFilesSelectedKinds()) {
  const selected = (Array.isArray(kinds) && kinds.length ? kinds : METADATA_MANAGER_KIND_FILTERS).filter(kind => METADATA_MANAGER_KIND_FILTERS.includes(kind));
  if (!selected.length || selected.length === METADATA_MANAGER_KIND_FILTERS.length) return 'Manuals, Strategy Guides, Magazines';
  return selected.map(kind => kind === 'Strategy Guide' ? 'Strategy Guides' : `${kind}s`).join(', ');
}

function syncServerFilesKindDropdown() {
  const summary = $('serverFilesKindSummary');
  if (summary) summary.textContent = serverFilesKindSummaryLabel();
  const dropdown = $('serverFilesKindDropdown');
  if (dropdown) dropdown.title = `Showing: ${serverFilesKindSummaryLabel()}`;
}

function serverFilesItemPath(item) {
  return String(item?.path || item?.Path || item?.sourcePath || item?.filePath || item?.relativePath || item?.RelativePath || '').trim();
}

function serverFilesBaseName(path = '') {
  const normalized = String(path || '').replace(/\\/g, '/');
  const parts = normalized.split('/').filter(Boolean);
  return parts.length ? parts[parts.length - 1] : String(path || '');
}

function serverFilesPathCodeHtml(path = '', emptyText = '\u2014') {
  const value = String(path || '').trim();
  if (!value) return `<span class="server-files-empty-path">${escapeHtml(emptyText)}</span>`;
  return `<code class="server-files-full-path" title="${escapeForAttribute(value)}">${escapeHtml(value)}</code>`;
}

function serverFilesPreviewPathBlockHtml(label = '', fileName = '', fullPath = '') {
  return `<div class="server-files-before-after-block">
    <span>${escapeHtml(label)}</span>
    <strong>${escapeHtml(fileName || serverFilesBaseName(fullPath) || '\u2014')}</strong>
    ${serverFilesPathCodeHtml(fullPath)}
  </div>`;
}

function serverFilesSearchText(item) {
  return [
    metadataManagerItemName(item),
    item?.title,
    item?.kind,
    item?.fileName,
    item?.filename,
    serverFilesItemPath(item),
    metadataManagerCategoryValue(item),
    metadataManagerSeriesValue(item),
    item?.publisher,
    item?.year,
    metadataManagerTopicValue(item),
    platformListText(item)
  ].join(' ').toLowerCase();
}

function serverFilesMatchingItems() {
  const files = serverFilesEnsureState();
  const selectedKinds = new Set(serverFilesSelectedKinds());
  const q = String(files.search || '').trim().toLowerCase();
  return (state.items || []).filter(item => {
    if (!METADATA_MANAGER_KIND_FILTERS.includes(item.kind)) return false;
    if (!selectedKinds.has(item.kind)) return false;
    if (q && !serverFilesSearchText(item).includes(q)) return false;
    return true;
  }).slice().sort((a, b) => {
    const kindCompare = String(a.kind || '').localeCompare(String(b.kind || ''), undefined, { sensitivity: 'base' });
    if (kindCompare) return kindCompare;
    const categoryCompare = String(metadataManagerCategoryValue(a) || metadataManagerSeriesValue(a) || '').localeCompare(String(metadataManagerCategoryValue(b) || metadataManagerSeriesValue(b) || ''), undefined, { numeric: true, sensitivity: 'base' });
    if (categoryCompare) return categoryCompare;
    return metadataManagerItemName(a).localeCompare(metadataManagerItemName(b), undefined, { numeric: true, sensitivity: 'base' });
  });
}

function serverFilesCurrentRenderLimit(total = 0) {
  const files = serverFilesEnsureState();
  const raw = Number(files.renderLimit || 0);
  const fallback = metadataManagerDefaultRenderLimit();
  const safe = Number.isFinite(raw) && raw > 0 ? raw : fallback;
  return Math.max(1, Math.min(Math.max(total, fallback), Math.floor(safe)));
}

function serverFilesCurrentSelectedIds() {
  const files = serverFilesEnsureState();
  return Array.from(new Set((files.selectedIds || []).map(id => String(id || '').trim()).filter(Boolean)));
}

function serverFilesSelectedItemsFromState() {
  const ids = new Set(serverFilesCurrentSelectedIds());
  return (state.items || []).filter(item => ids.has(metadataManagerItemId(item)));
}

function serverFilesSelectedSummaryText() {
  const files = serverFilesEnsureState();
  const items = serverFilesSelectedItemsFromState();
  const matched = serverFilesMatchingItems().length;
  if (!items.length) return `No files selected. Use the list below to select one file or a group of files. ${matched} matching file(s) available.`;
  const counts = items.reduce((acc, item) => { acc[item.kind || 'Other'] = (acc[item.kind || 'Other'] || 0) + 1; return acc; }, {});
  return `${items.length} selected for file actions: ${Object.entries(counts).map(([kind, count]) => `${count} ${kind}${count === 1 ? '' : 's'}`).join(', ')}. ${matched} matching file(s) shown by the current filters.`;
}

function serverFilesSetStatus(targetId, message = '', tone = '') {
  const el = $(targetId);
  if (!el) return;
  el.textContent = message;
  el.classList.toggle('success', tone === 'success');
  el.classList.toggle('error', tone === 'error');
}

function serverFilesPaintNextFrame() {
  return new Promise(resolve => {
    const frame = typeof requestAnimationFrame === 'function' ? requestAnimationFrame : callback => setTimeout(callback, 16);
    frame(() => frame(resolve));
  });
}

function serverFilesRefreshSummary() {
  const summary = $('serverFilesSummary');
  if (summary) summary.textContent = serverFilesSelectedSummaryText();
}

function serverFilesRowsHtml(items = []) {
  const selected = new Set(serverFilesCurrentSelectedIds());
  if (!items.length) return '<tr><td colspan="7" class="metadata-manager-empty">No files match the current Files workspace filter.</td></tr>';
  return items.map(item => {
    const id = metadataManagerItemId(item);
    const name = metadataManagerItemName(item) || item.title || item.fileName || 'Untitled';
    const category = item.kind === 'Magazine' ? (metadataManagerSeriesValue(item) || item.magazineTitle || '') : (metadataManagerCategoryValue(item) || platformListText(item) || '');
    const path = serverFilesItemPath(item) || item.fileName || '';
    const fileName = item.fileName || serverFilesBaseName(path) || '';
    const metadataStatus = metadataStatusOf(item) || '';
    return `<tr>
      <td><input class="server-files-row-check" type="checkbox" data-server-file-id="${escapeForAttribute(id)}" ${selected.has(id) ? 'checked' : ''} /></td>
      <td><span class="metadata-kind-pill metadata-kind-preview-trigger" data-metadata-preview-id="${escapeForAttribute(id)}" title="Click and hold to preview cover">${escapeHtml(item.kind || '')}</span></td>
      <td><strong>${escapeHtml(name)}</strong><br><small>${escapeHtml(metadataStatus || '\u2014')}</small></td>
      <td>${escapeHtml(category || '\u2014')}</td>
      <td><code class="server-files-file-name" title="${escapeForAttribute(fileName)}">${escapeHtml(fileName || '\u2014')}</code></td>
      <td>${serverFilesPathCodeHtml(path)}</td>
      <td>${escapeHtml(String(item.year || item.coverDate || item.publicationDate || '').trim() || '\u2014')}</td>
    </tr>`;
  }).join('');
}

function renderServerFilesWorkspace() {
  if (!$('settingsFilesPanel')) return;
  hydrateServerFilesTemplatePresets(false);
  const files = serverFilesEnsureState();
  if ($('serverFilesSearch')) $('serverFilesSearch').value = files.search || '';
  const selectedKinds = new Set(serverFilesSelectedKinds());
  document.querySelectorAll('[data-server-files-kind]').forEach(input => { input.checked = selectedKinds.has(input.dataset.serverFilesKind || ''); });
  syncServerFilesKindDropdown();
  const allItems = serverFilesMatchingItems();
  const limit = serverFilesCurrentRenderLimit(allItems.length);
  const rendered = allItems.slice(0, limit);
  const hidden = Math.max(0, allItems.length - rendered.length);
  const body = $('serverFilesTableBody');
  if (body) body.innerHTML = serverFilesRowsHtml(rendered) + (hidden ? `<tr><td colspan="7" class="metadata-manager-empty metadata-manager-hidden-note">${hidden} more matching file${hidden === 1 ? ' is' : 's are'} hidden by the display limit.</td></tr>` : '');
  const selected = new Set(serverFilesCurrentSelectedIds());
  const renderedIds = rendered.map(metadataManagerItemId).filter(Boolean);
  const header = $('serverFilesHeaderCheck');
  if (header) {
    header.checked = renderedIds.length > 0 && renderedIds.every(id => selected.has(id));
    header.indeterminate = renderedIds.some(id => selected.has(id)) && !header.checked;
  }
  if ($('serverFilesCount')) {
    $('serverFilesCount').textContent = hidden
      ? `${rendered.length} shown / ${allItems.length} matched (${hidden} not rendered yet)`
      : `${rendered.length} shown / ${allItems.length} matched`;
  }
  const loadMore = $('serverFilesLoadMore');
  if (loadMore) {
    loadMore.hidden = !hidden;
    loadMore.disabled = !hidden;
    loadMore.textContent = hidden ? `Load ${Math.min(METADATA_MANAGER_RENDER_STEP, hidden)} More` : 'Load More';
  }
  const showAll = $('serverFilesShowAll');
  if (showAll) {
    showAll.hidden = !hidden;
    showAll.disabled = !hidden;
    showAll.textContent = hidden ? `Show All ${allItems.length}` : 'Show All';
  }
  serverFilesRefreshSummary();
  renderServerFilesFormatTools();
}

function serverFilesScheduleRender(delay = METADATA_MANAGER_SEARCH_DEBOUNCE_MS) {
  const files = serverFilesEnsureState();
  if (files.renderTimer) clearTimeout(files.renderTimer);
  files.renderTimer = setTimeout(() => {
    files.renderTimer = null;
    renderServerFilesWorkspace();
  }, Math.max(0, Number(delay) || 0));
}

function serverFilesUpdateSearch(value) {
  const files = serverFilesEnsureState();
  files.search = String(value || '');
  files.renderLimit = metadataManagerDefaultRenderLimit();
  serverFilesScheduleRender();
}

function serverFilesUpdateKinds() {
  const files = serverFilesEnsureState();
  const kinds = Array.from(document.querySelectorAll('[data-server-files-kind]:checked')).map(input => input.dataset.serverFilesKind || '').filter(kind => METADATA_MANAGER_KIND_FILTERS.includes(kind));
  files.kindFilters = kinds.length ? kinds : METADATA_MANAGER_KIND_FILTERS.slice();
  files.renderLimit = metadataManagerDefaultRenderLimit();
  syncServerFilesKindDropdown();
  renderServerFilesWorkspace();
}

function serverFilesToggleSelection(id, checked) {
  if (!id) return;
  const files = serverFilesEnsureState();
  const selected = new Set(files.selectedIds || []);
  if (checked) selected.add(id); else selected.delete(id);
  files.selectedIds = Array.from(selected);
  renderServerFilesWorkspace();
}

function serverFilesSelectRendered(select = true) {
  const files = serverFilesEnsureState();
  const items = serverFilesMatchingItems();
  const limit = serverFilesCurrentRenderLimit(items.length);
  const ids = items.slice(0, limit).map(metadataManagerItemId).filter(Boolean);
  const selected = new Set(files.selectedIds || []);
  ids.forEach(id => select ? selected.add(id) : selected.delete(id));
  files.selectedIds = Array.from(selected);
  renderServerFilesWorkspace();
}

function serverFilesSelectMatching() {
  const files = serverFilesEnsureState();
  const selected = new Set(files.selectedIds || []);
  serverFilesMatchingItems().map(metadataManagerItemId).filter(Boolean).forEach(id => selected.add(id));
  files.selectedIds = Array.from(selected);
  renderServerFilesWorkspace();
}

function serverFilesClearSelection() {
  const files = serverFilesEnsureState();
  files.selectedIds = [];
  state.serverFilesPreview = [];
  state.serverFilesPreviewShowAll = false;
  if ($('serverFilesPreviewTable')) $('serverFilesPreviewTable').innerHTML = '';
  if ($('serverFilesWriteBackResults')) $('serverFilesWriteBackResults').innerHTML = '';
  if ($('serverFilesConvertResults')) $('serverFilesConvertResults').innerHTML = '';
  if ($('serverFilesApplyPreview')) $('serverFilesApplyPreview').disabled = true;
  renderServerFilesWorkspace();
}

function serverFilesUseMetadataSelection() {
  const ids = metadataManagerCurrentSelectedIds();
  const files = serverFilesEnsureState();
  files.selectedIds = ids;
  files.renderLimit = Math.max(files.renderLimit || metadataManagerDefaultRenderLimit(), ids.length, metadataManagerDefaultRenderLimit());
  renderServerFilesWorkspace();
  serverFilesSetStatus('serverFilesSelectionStatus', ids.length ? `Loaded ${ids.length} Metadata Manager selection(s) into Files.` : 'No Metadata Manager rows were selected.', ids.length ? 'success' : 'error');
}

function serverFilesLoadMoreRows() {
  const files = serverFilesEnsureState();
  const items = serverFilesMatchingItems();
  files.renderLimit = Math.min(items.length, serverFilesCurrentRenderLimit(items.length) + METADATA_MANAGER_RENDER_STEP);
  renderServerFilesWorkspace();
}

function serverFilesShowAllRows() {
  const files = serverFilesEnsureState();
  const items = serverFilesMatchingItems();
  files.renderLimit = Math.max(items.length, metadataManagerDefaultRenderLimit());
  renderServerFilesWorkspace();
}

function serverFilesTrackTemplateTarget(input) {
  if (!input?.id) return;
  const files = serverFilesEnsureState();
  files.templateTargetId = input.id;
  const kind = serverFilesTemplateKindFromInputId(input.id);
  files.templateKind = kind;
  const kindSelect = $('serverFilesTemplateKindSelect');
  if (kindSelect && kindSelect.value !== kind) {
    kindSelect.value = kind;
    renderServerFilesTemplatePresetOptions();
  }
}

function serverFilesTemplateKindDefinitions() {
  return {
    manual: {
      label: 'Manuals',
      singularLabel: 'Manual',
      inputId: 'serverFilesManualTemplate',
      defaultPresetId: 'guidevault-default-manual',
      defaultTemplate: 'Manuals/{Platform}/{GameTitle}/{Title} - Manual{Extension}'
    },
    strategyGuide: {
      label: 'Strategy Guides',
      singularLabel: 'Strategy Guide',
      inputId: 'serverFilesStrategyTemplate',
      defaultPresetId: 'guidevault-default-strategy-guide',
      defaultTemplate: 'Strategy Guides/{Platform}/{GameTitle}/{Title}{Extension}'
    },
    magazine: {
      label: 'Magazines',
      singularLabel: 'Magazine',
      inputId: 'serverFilesMagazineTemplate',
      defaultPresetId: 'guidevault-default-magazine',
      defaultTemplate: 'Magazines/{MagazineSeries}/{Year}/{MagazineSeries} - {IssuePart}{Extension}'
    }
  };
}

function serverFilesTemplateKindOrder() {
  return ['manual', 'strategyGuide', 'magazine'];
}

function serverFilesNormalizeTemplateKind(kind = '') {
  const key = String(kind || '').trim();
  return serverFilesTemplateKindDefinitions()[key] ? key : 'manual';
}

function serverFilesTemplateKindFromInputId(id = '') {
  const text = String(id || '');
  const defs = serverFilesTemplateKindDefinitions();
  return serverFilesTemplateKindOrder().find(kind => defs[kind].inputId === text) || 'manual';
}

function serverFilesTemplateInputForKind(kind = '') {
  const normalizedKind = serverFilesNormalizeTemplateKind(kind);
  const id = serverFilesTemplateKindDefinitions()[normalizedKind].inputId;
  return $(id);
}

function serverFilesCurrentTemplateKind() {
  const files = serverFilesEnsureState();
  return serverFilesNormalizeTemplateKind($('serverFilesTemplateKindSelect')?.value || files.templateKind || serverFilesTemplateKindFromInputId(files.templateTargetId || ''));
}

function serverFilesDefaultTemplatePresets(kind = '') {
  const presets = {
    manual: [
      { id: 'guidevault-default-manual', name: 'GuideVault Manual Default', template: 'Manuals/{Platform}/{GameTitle}/{Title} - Manual{Extension}', builtIn: true },
      { id: 'manual-flat-title', name: 'Manual Flat Title', template: '{Title}{Extension}', builtIn: true },
      { id: 'manual-publisher-year', name: 'Manual Publisher / Title / Year', template: '{Publisher}/{Title} - Manual - {Year}{Extension}', builtIn: true }
    ],
    strategyGuide: [
      { id: 'guidevault-default-strategy-guide', name: 'GuideVault Strategy Guide Default', template: 'Strategy Guides/{Platform}/{GameTitle}/{Title}{Extension}', builtIn: true },
      { id: 'strategy-guide-edition', name: 'Strategy Guide with Optional Edition', template: 'Strategy Guides/{Platform}/{GameTitle}/{Title} - {EditionPart}{Extension}', builtIn: true },
      { id: 'strategy-guide-publisher-isbn-year', name: 'Strategy Guide Publisher / ISBN / Year', template: '{Publisher}/{Title} - {GuideTypePart} - {ISBN10} - {Year}{Extension}', builtIn: true }
    ],
    magazine: [
      { id: 'guidevault-default-magazine', name: 'GuideVault Magazine Default', template: 'Magazines/{MagazineSeries}/{Year}/{MagazineSeries} - {IssuePart}{Extension}', builtIn: true },
      { id: 'magazine-volume-issue', name: 'Magazine with Optional Volume + Issue', template: 'Magazines/{MagazineSeries}/{Year}/{MagazineSeries} - {VolumePart} - {IssuePart}{Extension}', builtIn: true },
      { id: 'magazine-flat-series-issue', name: 'Magazine Flat Series + Issue', template: '{MagazineSeries} - {IssuePart}{Extension}', builtIn: true }
    ]
  };
  const normalizedKind = serverFilesNormalizeTemplateKind(kind);
  return kind ? presets[normalizedKind] : presets;
}

function serverFilesDefaultTemplates() {
  const defs = serverFilesTemplateKindDefinitions();
  return Object.fromEntries(serverFilesTemplateKindOrder().map(kind => [kind, defs[kind].defaultTemplate]));
}

function normalizeServerFilesTemplatePreset(value = {}, kind = 'manual', fallbackId = '') {
  const normalizedKind = serverFilesNormalizeTemplateKind(kind);
  const defs = serverFilesTemplateKindDefinitions();
  const defaultTemplate = defs[normalizedKind].defaultTemplate;
  const templates = value.templates || {};
  const template = String(value.template || templates[normalizedKind] || defaultTemplate || '').trim() || defaultTemplate;
  const id = String(value.id || fallbackId || `template-${normalizedKind}-${Date.now()}`).trim() || `template-${normalizedKind}-${Date.now()}`;
  const name = String(value.name || `${defs[normalizedKind].singularLabel} Template`).trim() || `${defs[normalizedKind].singularLabel} Template`;
  return {
    id,
    kind: normalizedKind,
    name,
    template,
    builtIn: value.builtIn === true,
    updatedAt: value.updatedAt || new Date().toISOString()
  };
}

function serverFilesBuildDefaultTemplatePresetState() {
  const presets = {};
  const selectedIds = {};
  const hiddenBuiltIns = {};
  serverFilesTemplateKindOrder().forEach(kind => {
    presets[kind] = {};
    hiddenBuiltIns[kind] = [];
    serverFilesDefaultTemplatePresets(kind).forEach(preset => {
      const normalized = normalizeServerFilesTemplatePreset(preset, kind, preset.id);
      presets[kind][normalized.id] = normalized;
    });
    selectedIds[kind] = serverFilesTemplateKindDefinitions()[kind].defaultPresetId;
  });
  return { selectedKind: 'manual', selectedIds, hiddenBuiltIns, presets };
}

function serverFilesApplyHiddenBuiltInTemplatePresets(stateValue) {
  serverFilesTemplateKindOrder().forEach(kind => {
    const hidden = new Set((stateValue.hiddenBuiltIns?.[kind] || []).map(id => String(id || '').trim()).filter(Boolean));
    hidden.forEach(id => {
      if (stateValue.presets?.[kind]?.[id]?.builtIn) delete stateValue.presets[kind][id];
    });
    if (!Object.keys(stateValue.presets?.[kind] || {}).length) {
      const fallback = normalizeServerFilesTemplatePreset(serverFilesDefaultTemplatePresets(kind)[0], kind, serverFilesTemplateKindDefinitions()[kind].defaultPresetId);
      stateValue.presets[kind] = { [fallback.id]: fallback };
      stateValue.selectedIds[kind] = fallback.id;
      stateValue.hiddenBuiltIns[kind] = [];
    }
  });
}

function serverFilesNormalizeTemplatePresetState(parsed = null) {
  const stateValue = serverFilesBuildDefaultTemplatePresetState();
  if (!parsed || typeof parsed !== 'object') return stateValue;

  if (parsed.selectedKind) stateValue.selectedKind = serverFilesNormalizeTemplateKind(parsed.selectedKind);
  if (parsed.hiddenBuiltIns && typeof parsed.hiddenBuiltIns === 'object') {
    serverFilesTemplateKindOrder().forEach(kind => {
      stateValue.hiddenBuiltIns[kind] = Array.isArray(parsed.hiddenBuiltIns[kind]) ? parsed.hiddenBuiltIns[kind].map(id => String(id || '').trim()).filter(Boolean) : [];
    });
  }
  serverFilesApplyHiddenBuiltInTemplatePresets(stateValue);

  if (parsed.presets && typeof parsed.presets === 'object') {
    serverFilesTemplateKindOrder().forEach(kind => {
      const kindPresets = parsed.presets[kind];
      if (!kindPresets || typeof kindPresets !== 'object') return;
      Object.entries(kindPresets).forEach(([id, preset]) => {
        const normalized = normalizeServerFilesTemplatePreset({ ...(preset || {}), id: preset?.id || id }, kind, id);
        if (normalized.builtIn && stateValue.hiddenBuiltIns[kind]?.includes(normalized.id)) return;
        stateValue.presets[kind][normalized.id] = normalized;
      });
    });
  }

  if (parsed.selectedIds && typeof parsed.selectedIds === 'object') {
    serverFilesTemplateKindOrder().forEach(kind => {
      const selectedId = String(parsed.selectedIds[kind] || '').trim();
      if (selectedId && stateValue.presets[kind]?.[selectedId]) stateValue.selectedIds[kind] = selectedId;
    });
  }

  serverFilesTemplateKindOrder().forEach(kind => {
    if (!stateValue.presets[kind]?.[stateValue.selectedIds[kind]]) {
      stateValue.selectedIds[kind] = Object.keys(stateValue.presets[kind] || {})[0] || serverFilesTemplateKindDefinitions()[kind].defaultPresetId;
    }
  });

  return stateValue;
}

function serverFilesMigrateLegacyTemplatePresetState() {
  try {
    const raw = localStorage.getItem(GUIDEVAULT_FILE_ORGANIZATION_TEMPLATE_PRESETS_LEGACY_KEY);
    if (!raw) return null;
    const legacy = JSON.parse(raw);
    if (!legacy || typeof legacy !== 'object' || !legacy.presets || typeof legacy.presets !== 'object') return null;
    const migrated = serverFilesBuildDefaultTemplatePresetState();
    Object.entries(legacy.presets).forEach(([id, preset]) => {
      if (!preset || preset.builtIn) return;
      serverFilesTemplateKindOrder().forEach(kind => {
        const defs = serverFilesTemplateKindDefinitions();
        const legacyTemplate = preset.templates?.[kind];
        if (!legacyTemplate) return;
        const migratedId = `migrated-${kind}-${String(id || Date.now()).replace(/[^a-z0-9_-]+/gi, '-')}`;
        const migratedName = `${String(preset.name || 'Custom Template').trim() || 'Custom Template'} - ${defs[kind].singularLabel}`;
        const normalized = normalizeServerFilesTemplatePreset({ id: migratedId, name: migratedName, template: legacyTemplate, builtIn: false, updatedAt: preset.updatedAt }, kind, migratedId);
        migrated.presets[kind][normalized.id] = normalized;
        if (legacy.selectedId && String(legacy.selectedId) === String(id)) migrated.selectedIds[kind] = normalized.id;
      });
    });
    return migrated;
  } catch { return null; }
}

function loadServerFilesTemplatePresets() {
  try {
    const raw = localStorage.getItem(GUIDEVAULT_FILE_ORGANIZATION_TEMPLATE_PRESETS_KEY);
    if (raw) return serverFilesNormalizeTemplatePresetState(JSON.parse(raw));
  } catch {}
  const migrated = serverFilesMigrateLegacyTemplatePresetState();
  if (migrated) {
    saveServerFilesTemplatePresets(migrated);
    return migrated;
  }
  return serverFilesBuildDefaultTemplatePresetState();
}

function saveServerFilesTemplatePresets(presetsState) {
  const normalized = serverFilesNormalizeTemplatePresetState(presetsState || loadServerFilesTemplatePresets());
  try { localStorage.setItem(GUIDEVAULT_FILE_ORGANIZATION_TEMPLATE_PRESETS_KEY, JSON.stringify(normalized)); } catch {}
  return normalized;
}

function serverFilesSetActiveTemplateKind(kind = '', options = {}) {
  const normalizedKind = serverFilesNormalizeTemplateKind(kind);
  const files = serverFilesEnsureState();
  files.templateKind = normalizedKind;
  files.templateTargetId = serverFilesTemplateKindDefinitions()[normalizedKind].inputId;
  const kindSelect = $('serverFilesTemplateKindSelect');
  if (kindSelect && kindSelect.value !== normalizedKind) kindSelect.value = normalizedKind;
  renderServerFilesTemplatePresetOptions();
  if (options.focusInput) {
    const input = serverFilesTemplateInputForKind(normalizedKind);
    if (input) {
      input.focus();
      try { input.setSelectionRange(input.value.length, input.value.length); } catch {}
    }
  }
}

function serverFilesSetTemplateInputs(templates = {}) {
  const defaults = serverFilesDefaultTemplates();
  const manual = $('serverFilesManualTemplate');
  const strategy = $('serverFilesStrategyTemplate');
  const magazine = $('serverFilesMagazineTemplate');
  if (manual) manual.value = String(templates.manual || defaults.manual || '').trim() || defaults.manual;
  if (strategy) strategy.value = String(templates.strategyGuide || defaults.strategyGuide || '').trim() || defaults.strategyGuide;
  if (magazine) magazine.value = String(templates.magazine || defaults.magazine || '').trim() || defaults.magazine;
  if ($('serverFilesApplyPreview')) $('serverFilesApplyPreview').disabled = true;
}

function serverFilesCurrentTemplateName(kind = '') {
  const normalizedKind = serverFilesNormalizeTemplateKind(kind || serverFilesCurrentTemplateKind());
  const defs = serverFilesTemplateKindDefinitions();
  const name = String($('serverFilesTemplatePresetName')?.value || '').trim();
  return name || `${defs[normalizedKind].singularLabel} Template`;
}

function renderServerFilesTemplatePresetOptions() {
  const select = $('serverFilesTemplatePresetSelect');
  if (!select) return;
  const presetsState = loadServerFilesTemplatePresets();
  const kind = serverFilesNormalizeTemplateKind($('serverFilesTemplateKindSelect')?.value || presetsState.selectedKind || 'manual');
  const kindSelect = $('serverFilesTemplateKindSelect');
  if (kindSelect && kindSelect.value !== kind) kindSelect.value = kind;

  const presets = Object.values(presetsState.presets?.[kind] || {}).sort((a, b) => {
    if (a.builtIn !== b.builtIn) return a.builtIn ? -1 : 1;
    return String(a.name || '').localeCompare(String(b.name || ''), undefined, { sensitivity: 'base' });
  });
  const groupHtml = (label, rows) => rows.length
    ? `<optgroup label="${escapeForAttribute(label)}">${rows.map(preset => `<option value="${escapeForAttribute(preset.id)}">${escapeHtml(preset.name || preset.id)}</option>`).join('')}</optgroup>`
    : '';
  select.innerHTML = groupHtml('Built-in templates', presets.filter(preset => preset.builtIn)) + groupHtml('Custom templates', presets.filter(preset => !preset.builtIn));
  const selectedId = presetsState.presets?.[kind]?.[presetsState.selectedIds?.[kind]] ? presetsState.selectedIds[kind] : presets[0]?.id;
  if (selectedId) select.value = selectedId;
  const selected = presetsState.presets?.[kind]?.[select.value];
  if ($('serverFilesTemplatePresetName') && selected) $('serverFilesTemplatePresetName').value = selected.name || '';
}

function serverFilesApplySelectedTemplatesToInputs(presetsState) {
  const stateValue = presetsState || loadServerFilesTemplatePresets();
  serverFilesTemplateKindOrder().forEach(kind => {
    const selectedId = stateValue.selectedIds?.[kind];
    const preset = stateValue.presets?.[kind]?.[selectedId] || Object.values(stateValue.presets?.[kind] || {})[0];
    const input = serverFilesTemplateInputForKind(kind);
    if (input && preset) input.value = String(preset.template || serverFilesTemplateKindDefinitions()[kind].defaultTemplate || '');
  });
  if ($('serverFilesApplyPreview')) $('serverFilesApplyPreview').disabled = true;
}

function hydrateServerFilesTemplatePresets(forceApply = false) {
  const select = $('serverFilesTemplatePresetSelect');
  if (!select) return;
  const firstHydrate = !select.dataset.hydrated;
  const presetsState = loadServerFilesTemplatePresets();
  const activeKind = serverFilesNormalizeTemplateKind(presetsState.selectedKind || 'manual');
  const kindSelect = $('serverFilesTemplateKindSelect');
  if (kindSelect) kindSelect.value = activeKind;
  const files = serverFilesEnsureState();
  files.templateKind = activeKind;
  files.templateTargetId = serverFilesTemplateKindDefinitions()[activeKind].inputId;
  renderServerFilesTemplatePresetOptions();
  if (forceApply || firstHydrate) serverFilesApplySelectedTemplatesToInputs(presetsState);
  select.dataset.hydrated = 'true';
}

function serverFilesApplySelectedTemplatePreset() {
  const select = $('serverFilesTemplatePresetSelect');
  if (!select) return;
  const kind = serverFilesCurrentTemplateKind();
  const presetsState = loadServerFilesTemplatePresets();
  const preset = presetsState.presets?.[kind]?.[select.value];
  if (!preset) return;
  presetsState.selectedKind = kind;
  presetsState.selectedIds[kind] = preset.id;
  saveServerFilesTemplatePresets(presetsState);
  if ($('serverFilesTemplatePresetName')) $('serverFilesTemplatePresetName').value = preset.name || '';
  const input = serverFilesTemplateInputForKind(kind);
  if (input) input.value = String(preset.template || '');
  if ($('serverFilesPreviewTable')) $('serverFilesPreviewTable').innerHTML = '<p class="sub">Template loaded. Preview selected files to see the updated before/after plan.</p>';
  serverFilesSetStatus('serverFilesOrganizeStatus', `Loaded ${serverFilesTemplateKindDefinitions()[kind].singularLabel.toLowerCase()} template: ${preset.name}.`, 'success');
}

function serverFilesSaveTemplatePreset() {
  const presetsState = loadServerFilesTemplatePresets();
  const select = $('serverFilesTemplatePresetSelect');
  const kind = serverFilesCurrentTemplateKind();
  const currentId = String(select?.value || '').trim();
  const existing = presetsState.presets?.[kind]?.[currentId];
  const name = serverFilesCurrentTemplateName(kind);
  const input = serverFilesTemplateInputForKind(kind);
  const template = String(input?.value || '').trim() || serverFilesTemplateKindDefinitions()[kind].defaultTemplate;
  const id = currentId && existing && !existing.builtIn ? currentId : `custom-${kind}-${Date.now()}`;
  const preset = normalizeServerFilesTemplatePreset({ id, name, template, builtIn: false }, kind, id);
  presetsState.presets[kind] = presetsState.presets[kind] || {};
  presetsState.presets[kind][id] = preset;
  presetsState.selectedKind = kind;
  presetsState.selectedIds[kind] = id;
  saveServerFilesTemplatePresets(presetsState);
  renderServerFilesTemplatePresetOptions();
  if ($('serverFilesTemplatePresetSelect')) $('serverFilesTemplatePresetSelect').value = id;
  if ($('serverFilesTemplatePresetName')) $('serverFilesTemplatePresetName').value = preset.name;
  serverFilesSetStatus('serverFilesOrganizeStatus', `Saved ${serverFilesTemplateKindDefinitions()[kind].singularLabel.toLowerCase()} template: ${preset.name}.`, 'success');
}

function serverFilesDeleteTemplatePreset() {
  const select = $('serverFilesTemplatePresetSelect');
  if (!select) return;
  const kind = serverFilesCurrentTemplateKind();
  const presetsState = loadServerFilesTemplatePresets();
  const id = String(select.value || '').trim();
  const preset = presetsState.presets?.[kind]?.[id];
  if (!preset) return;
  const visibleCount = Object.keys(presetsState.presets?.[kind] || {}).length;
  if (visibleCount <= 1) {
    serverFilesSetStatus('serverFilesOrganizeStatus', `Keep at least one ${serverFilesTemplateKindDefinitions()[kind].singularLabel.toLowerCase()} template available.`, 'error');
    return;
  }
  if (preset.builtIn) {
    presetsState.hiddenBuiltIns[kind] = Array.from(new Set([...(presetsState.hiddenBuiltIns?.[kind] || []), id]));
  }
  delete presetsState.presets[kind][id];
  presetsState.selectedKind = kind;
  presetsState.selectedIds[kind] = Object.keys(presetsState.presets[kind] || {})[0] || serverFilesTemplateKindDefinitions()[kind].defaultPresetId;
  saveServerFilesTemplatePresets(presetsState);
  hydrateServerFilesTemplatePresets(false);
  serverFilesSetStatus('serverFilesOrganizeStatus', `Deleted ${serverFilesTemplateKindDefinitions()[kind].singularLabel.toLowerCase()} template: ${preset.name}.`, 'success');
}

function serverFilesCurrentTemplateInput() {
  const files = serverFilesEnsureState();
  const targetId = files.templateTargetId || document.activeElement?.id || 'serverFilesManualTemplate';
  const input = $(targetId);
  if (input && input.classList?.contains('server-files-template-input')) return input;
  return $('serverFilesManualTemplate') || $('serverFilesStrategyTemplate') || $('serverFilesMagazineTemplate');
}

function serverFilesInsertTemplateToken(token = '') {
  const input = serverFilesCurrentTemplateInput();
  if (!input || !token) return;
  const value = String(input.value || '');
  const start = Number.isFinite(input.selectionStart) ? input.selectionStart : value.length;
  const end = Number.isFinite(input.selectionEnd) ? input.selectionEnd : start;
  input.value = `${value.slice(0, start)}${token}${value.slice(end)}`;
  const nextPos = start + token.length;
  input.focus();
  try { input.setSelectionRange(nextPos, nextPos); } catch {}
  serverFilesTrackTemplateTarget(input);
  if ($('serverFilesApplyPreview')) $('serverFilesApplyPreview').disabled = true;
  if ($('serverFilesPreviewTable')) $('serverFilesPreviewTable').innerHTML = '<p class="sub">Template changed. Preview selected files again to see the updated before/after plan.</p>';
}

function serverFilesTemplatesPayload() {
  const defaults = serverFilesDefaultTemplates();
  return {
    manual: String($('serverFilesManualTemplate')?.value || '').trim() || defaults.manual,
    strategyGuide: String($('serverFilesStrategyTemplate')?.value || '').trim() || defaults.strategyGuide,
    magazine: String($('serverFilesMagazineTemplate')?.value || '').trim() || defaults.magazine
  };
}

function serverFilesPreviewRowsHtml(rows = [], options = {}) {
  if (!rows.length) return '<p class="sub">No preview rows yet.</p>';
  const total = rows.length;
  const showAll = options.showAll === true || total <= SERVER_FILES_PREVIEW_RENDER_LIMIT;
  const visibleRows = showAll ? rows : rows.slice(0, SERVER_FILES_PREVIEW_RENDER_LIMIT);
  const note = total > visibleRows.length
    ? `<div class="server-files-preview-note"><strong>Showing ${visibleRows.length} of ${total} preview rows for speed.</strong><span>Apply Previewed Moves still uses the full validated preview result.</span><button class="ghost" type="button" data-server-files-preview-show-all="true">Show All Rows</button></div>`
    : '';
  return `${note}<table class="server-files-before-after-table"><thead><tr><th>Type</th><th>Title</th><th>Before</th><th>After</th><th>Status</th></tr></thead><tbody>${visibleRows.map(row => {
    const currentPath = row.currentPath || '';
    const proposedPath = row.proposedPath || '';
    const currentName = row.fileName || serverFilesBaseName(currentPath);
    const proposedName = serverFilesBaseName(proposedPath);
    return `<tr class="server-files-row-${escapeForAttribute(String(row.status || '').toLowerCase().replace(/[^a-z0-9]+/g, '-'))}">
      <td>${escapeHtml(row.kind || '')}</td>
      <td>${escapeHtml(row.title || row.fileName || '')}</td>
      <td>${serverFilesPreviewPathBlockHtml('Current file', currentName, currentPath)}</td>
      <td>${serverFilesPreviewPathBlockHtml('Proposed file', proposedName, proposedPath)}</td>
      <td>${escapeHtml(row.status || '')}${row.message ? `<br><small>${escapeHtml(row.message)}</small>` : ''}</td>
    </tr>`;
  }).join('')}</tbody></table>`;
}

function serverFilesRenderPreviewRows(showAll = false) {
  const table = $('serverFilesPreviewTable');
  if (!table) return;
  state.serverFilesPreviewShowAll = !!showAll;
  table.innerHTML = serverFilesPreviewRowsHtml(state.serverFilesPreview || [], { showAll: state.serverFilesPreviewShowAll });
}

async function serverFilesPreviewSelected() {
  const ids = serverFilesCurrentSelectedIds();
  const previewBtn = $('serverFilesPreviewSelected');
  const applyBtn = $('serverFilesApplyPreview');
  serverFilesRefreshSummary();
  if (!ids.length) {
    serverFilesSetStatus('serverFilesOrganizeStatus', 'Select one or more files from the Files workspace list first.', 'error');
    return;
  }
  state.serverFilesPreviewShowAll = false;
  state.serverFilesPreview = [];
  if (previewBtn) previewBtn.disabled = true;
  if (applyBtn) applyBtn.disabled = true;
  if ($('serverFilesPreviewTable')) $('serverFilesPreviewTable').innerHTML = `<div class="server-files-preview-loading"><strong>Building preview for ${ids.length} selected file(s)...</strong><span>Checking proposed paths and conflicts without moving anything.</span></div>`;
  serverFilesSetStatus('serverFilesOrganizeStatus', `Building preview for ${ids.length} selected file(s)...`, '');
  await serverFilesPaintNextFrame();
  try {
    const res = await fetch('/api/items/files/organize/preview', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ids, templates: serverFilesTemplatesPayload() })
    });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || `Preview failed. HTTP ${res.status}`);
    state.serverFilesPreview = data?.results || [];
    serverFilesRenderPreviewRows(false);
    if (applyBtn) applyBtn.disabled = !(data?.readyToApply > 0);
    const cappedMessage = state.serverFilesPreview.length > SERVER_FILES_PREVIEW_RENDER_LIMIT
      ? ` Showing the first ${SERVER_FILES_PREVIEW_RENDER_LIMIT} rows in the table for speed.`
      : '';
    serverFilesSetStatus('serverFilesOrganizeStatus', `${data?.message || `Previewed ${state.serverFilesPreview.length} file(s).`}${cappedMessage}`, data?.readyToApply ? 'success' : '');
  } finally {
    if (previewBtn) previewBtn.disabled = false;
  }
}

async function serverFilesApplyPreview() {
  const ids = serverFilesCurrentSelectedIds();
  if (!ids.length) {
    serverFilesSetStatus('serverFilesOrganizeStatus', 'Select one or more files from the Files workspace list first.', 'error');
    return;
  }
  const confirmed = await showAppConfirm({
    title: 'Apply file moves?',
    message: `GuideVault will rename and/or move ${ids.length} selected source file(s) inside their current library roots. Existing destination files will not be overwritten.`,
    okText: 'Apply Moves',
    cancelText: 'Cancel'
  });
  if (!confirmed) return;
  serverFilesSetStatus('serverFilesOrganizeStatus', `Applying file organization to ${ids.length} selected file(s)...`, '');
  const res = await fetch('/api/items/files/organize/apply', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ids, templates: serverFilesTemplatesPayload() })
  });
  let data = null;
  try { data = await res.json(); } catch {}
  if (!res.ok) throw new Error(data?.error || `Apply failed. HTTP ${res.status}`);
  state.serverFilesPreview = data?.results || [];
  if ($('serverFilesPreviewTable')) $('serverFilesPreviewTable').innerHTML = serverFilesPreviewRowsHtml(state.serverFilesPreview);
  const updatedItems = Array.isArray(data?.items) ? data.items : [];
  updatedItems.forEach(item => replaceItemInState(item));
  if (updatedItems.length) {
    markLibraryIndexesDirty();
    applyFilters();
    renderServerFilesWorkspace();
    if (state.selected) {
      const selectedId = metadataManagerItemId(state.selected);
      const replacement = updatedItems.find(item => metadataManagerItemId(item) === selectedId);
      if (replacement) renderDetails(replacement);
    }
  }
  serverFilesSetStatus('serverFilesOrganizeStatus', data?.message || `Moved ${data?.moved || 0} file(s).`, data?.failed ? 'error' : 'success');
}

function serverFilesWriteBackRowsHtml(rows = []) {
  if (!rows.length) return '<p class="sub">No write-back results yet.</p>';
  return `<table><thead><tr><th>Type</th><th>Title</th><th>File</th><th>Status</th><th>Message</th></tr></thead><tbody>${rows.map(row => `
    <tr class="server-files-row-${row.success ? 'ready' : 'conflict'}">
      <td>${escapeHtml(row.kind || '')}</td>
      <td>${escapeHtml(row.title || '')}</td>
      <td>${escapeHtml(row.writtenArchiveFileName || row.fileName || '')}</td>
      <td>${escapeHtml(row.success ? 'Written' : 'Failed')}</td>
      <td>${escapeHtml(row.message || '')}</td>
    </tr>`).join('')}</tbody></table>`;
}


function serverFilesFormatLabel(item) {
  const raw = String(item?.format || item?.fileFormat || '').trim() || String(serverFilesItemPath(item).split('.').pop() || '').trim();
  return raw ? raw.toUpperCase().replace(/^\./, '') : 'Unknown';
}

function serverFilesFormatSummaryHtml(items = []) {
  if (!items.length) return '<strong>Current Format</strong><span>Select files above to see their current formats.</span>';
  const counts = items.reduce((map, item) => {
    const label = serverFilesFormatLabel(item);
    map[label] = (map[label] || 0) + 1;
    return map;
  }, {});
  const chips = Object.entries(counts)
    .sort((a, b) => a[0].localeCompare(b[0]))
    .map(([format, count]) => `<span class="server-files-format-chip"><b>${escapeHtml(format)}</b>${count} file${count === 1 ? '' : 's'}</span>`)
    .join('');
  return `<strong>Current Format</strong><div class="server-files-format-chip-row">${chips}</div><em>${items.length} selected file${items.length === 1 ? '' : 's'} ready for conversion actions.</em>`;
}

function renderServerFilesFormatTools() {
  const summary = $('serverFilesFormatSummary');
  if (!summary) return;
  const items = serverFilesSelectedItemsFromState();
  summary.innerHTML = serverFilesFormatSummaryHtml(items);
  const hasSelection = items.length > 0;
  ['serverFilesConvertCbz','serverFilesConvertPdf'].forEach(id => { if ($(id)) $(id).disabled = !hasSelection; });
}

function serverFilesFormatBytes(bytes) {
  const value = Number(bytes || 0);
  if (!Number.isFinite(value) || value <= 0) return '\u2014';
  const units = ['B','KB','MB','GB','TB'];
  let size = value;
  let unit = 0;
  while (size >= 1024 && unit < units.length - 1) { size /= 1024; unit++; }
  return `${size.toFixed(size >= 10 || unit === 0 ? 0 : 1)} ${units[unit]}`;
}

function serverFilesConvertRowsHtml(rows = []) {
  if (!rows.length) return '<p class="sub">No conversion results yet.</p>';
  return `<table><thead><tr><th>Type</th><th>Title</th><th>From</th><th>To</th><th>Size</th><th>Status</th></tr></thead><tbody>${rows.map(row => `
    <tr class="server-files-row-${row.success ? 'ready' : 'conflict'}">
      <td>${escapeHtml(row.kind || '')}</td>
      <td>${escapeHtml(row.title || '')}</td>
      <td>${escapeHtml(row.sourceFormat || '')}<br><small>${escapeHtml(row.sourceFileName || row.fileName || '')}</small></td>
      <td>${escapeHtml(row.targetFormat || '')}<br><small>${escapeHtml(row.outputFileName || '\u2014')}</small></td>
      <td>${serverFilesFormatBytes(row.sourceBytes)} &rarr; ${serverFilesFormatBytes(row.outputBytes)}</td>
      <td>${escapeHtml(row.success ? 'Created' : 'Failed')}${row.message ? `<br><small>${escapeHtml(row.message)}</small>` : ''}</td>
    </tr>`).join('')}</tbody></table>`;
}

async function serverFilesConvertSelected(targetFormat = 'cbz') {
  const ids = serverFilesCurrentSelectedIds();
  if (!ids.length) {
    serverFilesSetStatus('serverFilesConvertStatus', 'Select one or more files from the Files workspace list first.', 'error');
    return;
  }
  const labels = { cbz: 'CBZ', pdf: 'PDF' };
  const actionLabel = labels[targetFormat] || targetFormat.toUpperCase();
  const confirmed = await showAppConfirm({
    title: `Create ${actionLabel} copies?`,
    message: `GuideVault will create ${actionLabel} copy files beside ${ids.length} selected source file(s). The original files will not be deleted or replaced.`,
    okText: `Create ${actionLabel}`,
    cancelText: 'Cancel'
  });
  if (!confirmed) return;
  const buttons = ['serverFilesConvertCbz','serverFilesConvertPdf'].map(id => $(id)).filter(Boolean);
  buttons.forEach(btn => { btn.disabled = true; });
  serverFilesSetStatus('serverFilesConvertStatus', `Creating ${actionLabel} copy files for ${ids.length} selected item(s)...`, '');
  if ($('serverFilesConvertResults')) $('serverFilesConvertResults').innerHTML = `<div class="server-files-preview-loading"><strong>Converting ${ids.length} selected file(s)...</strong><span>Large image archives and PDFs may take a while because pages have to be read, rasterized, and rewritten.</span></div>`;
  try {
    const res = await fetch('/api/items/files/convert', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ids, targetFormat })
    });
    let data = null;
    try { data = await res.json(); } catch {}
    if (!res.ok) throw new Error(data?.error || `Conversion failed. HTTP ${res.status}`);
    if ($('serverFilesConvertResults')) $('serverFilesConvertResults').innerHTML = serverFilesConvertRowsHtml(data?.results || []);
    serverFilesSetStatus('serverFilesConvertStatus', data?.message || `Created ${data?.converted || 0} converted file(s).`, data?.failed ? 'error' : 'success');
  } finally {
    renderServerFilesFormatTools();
  }
}

async function metadataManagerWriteBackSelected(source = 'metadata') {
  const ids = source === 'files' ? serverFilesCurrentSelectedIds() : metadataManagerCurrentSelectedIds();
  if (!ids.length) {
    if (source === 'files') serverFilesSetStatus('serverFilesWriteBackStatus', 'Select one or more files from the Files workspace list first.', 'error');
    else metadataManagerSetStatus('Select one or more rows first.', 'error');
    return;
  }
  const dirtyIds = new Set(Object.keys(state.metadataManager?.dirty || {}));
  const selectedDirty = ids.filter(id => dirtyIds.has(id));
  if (selectedDirty.length) await metadataManagerSaveDirtyRows();
  const statusTarget = source === 'files' ? 'serverFilesWriteBackStatus' : null;
  if (statusTarget) serverFilesSetStatus(statusTarget, `Writing Guidevault JSON to ${ids.length} selected file(s)...`, '');
  else metadataManagerSetStatus(`Writing Guidevault JSON to ${ids.length} selected file(s)...`);
  const res = await fetch('/api/items/metadata/native-export/bulk', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ids })
  });
  let data = null;
  try { data = await res.json(); } catch {}
  if (!res.ok) throw new Error(data?.error || `Write-back failed. HTTP ${res.status}`);
  const message = data?.message || `Wrote Guidevault JSON for ${data?.written || 0} file(s).`;
  if (statusTarget) {
    serverFilesSetStatus(statusTarget, message, data?.failed ? 'error' : 'success');
    if ($('serverFilesWriteBackResults')) $('serverFilesWriteBackResults').innerHTML = serverFilesWriteBackRowsHtml(data?.results || []);
  } else {
    metadataManagerSetStatus(message, data?.failed ? 'error' : 'success');
  }
}

function metadataManagerOpenFilesWorkspace() {
  const ids = metadataManagerCurrentSelectedIds();
  if (ids.length) {
    const files = serverFilesEnsureState();
    files.selectedIds = ids;
    files.renderLimit = Math.max(files.renderLimit || metadataManagerDefaultRenderLimit(), ids.length, metadataManagerDefaultRenderLimit());
  }
  activateSettingsTab('files');
  renderServerFilesWorkspace();
}

if ($('integrationsSaveSettings')) $('integrationsSaveSettings').addEventListener('click', e => { e.preventDefault(); saveServerSettings('integrations'); });
if ($('igdbTestCredentials')) $('igdbTestCredentials').addEventListener('click', e => { e.preventDefault(); testIgdbCredentials(); });
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
if ($('systemEventsRefresh')) $('systemEventsRefresh').addEventListener('click', e => { e.preventDefault(); loadSystemEvents(true); });
if ($('usersRefresh')) $('usersRefresh').addEventListener('click', e => { e.preventDefault(); scheduleUsersSettingsLoad(true, 0); });
if ($('usersInviteButton')) $('usersInviteButton').addEventListener('click', e => { e.preventDefault(); inviteUser(); });
if ($('tasksSaveSettings')) $('tasksSaveSettings').addEventListener('click', e => { e.preventDefault(); saveTaskSettings(); });
if ($('taskRunRescan')) $('taskRunRescan').addEventListener('click', async e => { e.preventDefault(); setTasksSettingsStatus('Rescan queued.', 'info'); await rescanLibrary(); });
if ($('taskRunEnrich')) $('taskRunEnrich').addEventListener('click', async e => { e.preventDefault(); setTasksSettingsStatus('Fast metadata enrichment queued.', 'info'); await enrichLibraryMetadata(); });
if ($('taskRunComicInfo')) $('taskRunComicInfo').addEventListener('click', async e => { e.preventDefault(); setTasksSettingsStatus('Legacy ComicInfo import queued.', 'info'); await importLegacyComicInfoMetadata(); });
if ($('taskRunCleanup')) $('taskRunCleanup').addEventListener('click', async e => { e.preventDefault(); setTasksSettingsStatus('Cleanup queued.', 'info'); await cleanupLibrary(); });
if ($('taskRunBackup')) $('taskRunBackup').addEventListener('click', e => { e.preventDefault(); createServerBackup(); });
if ($('taskRunTrim')) $('taskRunTrim').addEventListener('click', e => { e.preventDefault(); trimGuidevaultMemory(); setTasksSettingsStatus('Reading cache clear requested.', 'info'); });

if ($('metadataManagerSearch')) $('metadataManagerSearch').addEventListener('input', e => metadataManagerUpdateFilter('search', e.currentTarget.value));
if ($('metadataManagerKind')) $('metadataManagerKind').addEventListener('change', e => { metadataManagerSetSelectedKinds(e.currentTarget.value ? [e.currentTarget.value] : METADATA_MANAGER_KIND_FILTERS); metadataManagerUpdateFilter('kindFilters', state.metadataManager.kindFilters); });
document.querySelectorAll('[data-metadata-manager-kind]').forEach(input => input.addEventListener('change', () => {
  const kinds = Array.from(document.querySelectorAll('[data-metadata-manager-kind]:checked')).map(el => el.dataset.metadataManagerKind || '').filter(Boolean);
  metadataManagerSetSelectedKinds(kinds);
  metadataManagerUpdateFilter('kindFilters', state.metadataManager.kindFilters);
}));
if ($('metadataManagerStatusFilter')) $('metadataManagerStatusFilter').addEventListener('change', e => metadataManagerUpdateFilter('statusFilter', e.currentTarget.value));
if ($('metadataManagerMissing')) $('metadataManagerMissing').addEventListener('change', e => metadataManagerUpdateFilter('missing', e.currentTarget.value));
if ($('metadataManagerCategory')) $('metadataManagerCategory').addEventListener('change', e => metadataManagerUpdateFilter('category', e.currentTarget.value));
if ($('metadataManagerRefresh')) $('metadataManagerRefresh').addEventListener('click', () => { const picker = $('metadataManagerColumnPicker'); if (picker) picker.innerHTML = ''; const cat = $('metadataManagerCategory'); if (cat) cat.innerHTML = ''; metadataManagerResetRenderLimit(); renderMetadataManager(); metadataManagerSetStatus('Metadata grid refreshed.', 'success'); });
if ($('metadataManagerLoadMoreRows')) $('metadataManagerLoadMoreRows').addEventListener('click', metadataManagerLoadMoreRows);
if ($('metadataManagerShowAllRows')) $('metadataManagerShowAllRows').addEventListener('click', metadataManagerShowAllRows);
if ($('metadataManagerCollapseRows')) $('metadataManagerCollapseRows').addEventListener('click', metadataManagerCollapseRows);
if ($('metadataManagerSelectAll')) $('metadataManagerSelectAll').addEventListener('click', () => metadataManagerSelectVisible(true));
if ($('metadataManagerClearSelection')) $('metadataManagerClearSelection').addEventListener('click', () => { state.metadataManager.selectedIds = []; renderMetadataManager(); });
if ($('metadataManagerApplyBatch')) $('metadataManagerApplyBatch').addEventListener('click', async () => { try { await metadataManagerApplyBatch(); } catch (err) { console.error(err); metadataManagerSetStatus(`Batch apply failed: ${err?.message || err}`, 'error'); } });
if ($('metadataManagerNormalizeSelected')) $('metadataManagerNormalizeSelected').addEventListener('click', async () => { try { await metadataManagerNormalizeSelected(); } catch (err) { console.error(err); metadataManagerSetStatus(`Normalize failed: ${err?.message || err}`, 'error'); } });
if ($('metadataManagerSaveDirty')) $('metadataManagerSaveDirty').addEventListener('click', async () => { try { await metadataManagerSaveDirtyRows(); } catch (err) { console.error(err); metadataManagerSetStatus(`Save failed: ${err?.message || err}`, 'error'); } });
if ($('metadataManagerExportCsv')) $('metadataManagerExportCsv').addEventListener('click', metadataManagerExportCsv);
if ($('metadataManagerImportJson')) $('metadataManagerImportJson').addEventListener('click', () => $('metadataManagerImportFile')?.click());
if ($('metadataManagerImportFile')) $('metadataManagerImportFile').addEventListener('change', e => metadataManagerImportJsonFile(e.currentTarget.files?.[0]));
if ($('metadataManagerScrape')) $('metadataManagerScrape').addEventListener('click', metadataManagerScrapePlaceholder);
if ($('metadataManagerWriteBackSelected')) $('metadataManagerWriteBackSelected').addEventListener('click', async () => { try { await metadataManagerWriteBackSelected('metadata'); } catch (err) { console.error(err); metadataManagerSetStatus(`Write-back failed: ${err?.message || err}`, 'error'); } });
if ($('metadataManagerOpenFilesWorkspace')) $('metadataManagerOpenFilesWorkspace').addEventListener('click', metadataManagerOpenFilesWorkspace);
if ($('serverFilesRefreshSelection')) $('serverFilesRefreshSelection').addEventListener('click', renderServerFilesWorkspace);
if ($('serverFilesSearch')) $('serverFilesSearch').addEventListener('input', e => serverFilesUpdateSearch(e.currentTarget.value));
document.querySelectorAll('[data-server-files-kind]').forEach(input => input.addEventListener('change', serverFilesUpdateKinds));
if ($('serverFilesTemplateKindSelect')) $('serverFilesTemplateKindSelect').addEventListener('change', e => serverFilesSetActiveTemplateKind(e.currentTarget.value, { focusInput: true }));
if ($('serverFilesTemplatePresetSelect')) $('serverFilesTemplatePresetSelect').addEventListener('change', serverFilesApplySelectedTemplatePreset);
if ($('serverFilesSaveTemplatePreset')) $('serverFilesSaveTemplatePreset').addEventListener('click', e => { e.preventDefault(); serverFilesSaveTemplatePreset(); });
if ($('serverFilesDeleteTemplatePreset')) $('serverFilesDeleteTemplatePreset').addEventListener('click', e => { e.preventDefault(); serverFilesDeleteTemplatePreset(); });
if ($('serverFilesHeaderCheck')) $('serverFilesHeaderCheck').addEventListener('change', e => serverFilesSelectRendered(!!e.currentTarget.checked));
if ($('serverFilesTableBody')) $('serverFilesTableBody').addEventListener('change', e => {
  const check = e.target.closest?.('.server-files-row-check');
  if (!check) return;
  serverFilesToggleSelection(check.dataset.serverFileId || '', !!check.checked);
});
if ($('serverFilesSelectVisible')) $('serverFilesSelectVisible').addEventListener('click', () => serverFilesSelectRendered(true));
if ($('serverFilesSelectMatching')) $('serverFilesSelectMatching').addEventListener('click', serverFilesSelectMatching);
if ($('serverFilesClearSelection')) $('serverFilesClearSelection').addEventListener('click', serverFilesClearSelection);
if ($('serverFilesUseMetadataSelection')) $('serverFilesUseMetadataSelection').addEventListener('click', serverFilesUseMetadataSelection);
if ($('serverFilesLoadMore')) $('serverFilesLoadMore').addEventListener('click', serverFilesLoadMoreRows);
if ($('serverFilesShowAll')) $('serverFilesShowAll').addEventListener('click', serverFilesShowAllRows);
document.querySelectorAll('.server-files-template-input').forEach(input => {
  input.addEventListener('focus', () => serverFilesTrackTemplateTarget(input));
  input.addEventListener('click', () => serverFilesTrackTemplateTarget(input));
  input.addEventListener('input', () => {
    if ($('serverFilesApplyPreview')) $('serverFilesApplyPreview').disabled = true;
    if ($('serverFilesPreviewTable')) $('serverFilesPreviewTable').innerHTML = '<p class="sub">Template changed. Preview selected files again to see the updated before/after plan.</p>';
  });
});
document.querySelectorAll('[data-server-files-token]').forEach(btn => btn.addEventListener('click', e => {
  e.preventDefault();
  serverFilesInsertTemplateToken(btn.dataset.serverFilesToken || '');
}));

if ($('serverFilesPreviewTable')) $('serverFilesPreviewTable').addEventListener('click', e => {
  const btn = e.target.closest?.('[data-server-files-preview-show-all]');
  if (!btn) return;
  e.preventDefault();
  serverFilesRenderPreviewRows(true);
  serverFilesSetStatus('serverFilesOrganizeStatus', `Showing all ${(state.serverFilesPreview || []).length} preview row(s).`, 'success');
});
if ($('serverFilesPreviewSelected')) $('serverFilesPreviewSelected').addEventListener('click', async () => { try { await serverFilesPreviewSelected(); } catch (err) { console.error(err); serverFilesSetStatus('serverFilesOrganizeStatus', `Preview failed: ${err?.message || err}`, 'error'); if ($('serverFilesPreviewTable')) $('serverFilesPreviewTable').innerHTML = `<p class="sub">Preview failed: ${escapeHtml(err?.message || String(err || 'Unknown error'))}</p>`; if ($('serverFilesApplyPreview')) $('serverFilesApplyPreview').disabled = true; } });
if ($('serverFilesApplyPreview')) $('serverFilesApplyPreview').addEventListener('click', async () => { try { await serverFilesApplyPreview(); } catch (err) { console.error(err); serverFilesSetStatus('serverFilesOrganizeStatus', `Apply failed: ${err?.message || err}`, 'error'); } });
if ($('serverFilesWriteBackSelected')) $('serverFilesWriteBackSelected').addEventListener('click', async () => { try { await metadataManagerWriteBackSelected('files'); } catch (err) { console.error(err); serverFilesSetStatus('serverFilesWriteBackStatus', `Write-back failed: ${err?.message || err}`, 'error'); } });
if ($('serverFilesConvertCbz')) $('serverFilesConvertCbz').addEventListener('click', async () => { try { await serverFilesConvertSelected('cbz'); } catch (err) { console.error(err); serverFilesSetStatus('serverFilesConvertStatus', `Conversion failed: ${err?.message || err}`, 'error'); renderServerFilesFormatTools(); } });
if ($('serverFilesConvertPdf')) $('serverFilesConvertPdf').addEventListener('click', async () => { try { await serverFilesConvertSelected('pdf'); } catch (err) { console.error(err); serverFilesSetStatus('serverFilesConvertStatus', `Conversion failed: ${err?.message || err}`, 'error'); renderServerFilesFormatTools(); } });
if ($('metadataManagerRunSourceLookup')) $('metadataManagerRunSourceLookup').addEventListener('click', async e => { e.preventDefault(); try { await metadataManagerRunBatchSourceLookup(); } catch (err) { console.error(err); metadataManagerSetStatus(`Batch source lookup failed: ${err?.message || err}`, 'error'); } });
if ($('metadataManagerApplySourceLookup')) $('metadataManagerApplySourceLookup').addEventListener('click', async e => { e.preventDefault(); try { await metadataManagerApplyBatchSourceResults(); } catch (err) { console.error(err); metadataManagerSetStatus(`Apply batch lookup failed: ${err?.message || err}`, 'error'); } });
document.querySelectorAll('[data-metadata-batch-select-fields]').forEach(btn => btn.addEventListener('click', e => { e.preventDefault(); metadataBatchSelectAllFields(btn.dataset.metadataBatchSelectFields || '', true); }));
document.querySelectorAll('[data-metadata-batch-clear-fields]').forEach(btn => btn.addEventListener('click', e => { e.preventDefault(); metadataBatchSelectAllFields(btn.dataset.metadataBatchClearFields || '', false); }));
if ($('metadataBatchSourceResults')) $('metadataBatchSourceResults').addEventListener('change', e => { if (e.target?.classList?.contains('metadata-batch-row-check')) { const row = (state.metadataSourceBatch?.results || []).find(r => r.id === e.target.dataset.batchId); if (row) row.checked = !!e.target.checked; } });
document.querySelectorAll('input[data-metadata-batch-source-toggle]').forEach(el => el.addEventListener('change', e => { metadataBatchSyncSourceLocks(e.currentTarget); renderMetadataBatchSourceResults(); }));
document.querySelectorAll('input[data-metadata-batch-field], #metadataBatchSourceImportMode').forEach(el => el.addEventListener('change', renderMetadataBatchSourceResults));
metadataBatchSyncSourceLocks();
if ($('metadataManagerColumnPicker')) $('metadataManagerColumnPicker').addEventListener('change', e => {
  const target = e.target;
  if (target?.matches?.('input[data-column-key]')) metadataManagerSetColumnVisible(target.dataset.columnKey || '', !!target.checked);
});
if ($('metadataManagerColumnPicker')) $('metadataManagerColumnPicker').addEventListener('click', e => {
  const btn = e.target.closest?.('[data-column-move]');
  if (btn) { e.preventDefault(); metadataManagerMoveColumn(btn.dataset.columnKey || '', Number(btn.dataset.columnMove || 0)); }
});
if ($('metadataManagerHeaderRow')) {
  $('metadataManagerHeaderRow').addEventListener('click', metadataManagerHandleHeaderClick);
  $('metadataManagerHeaderRow').addEventListener('dragstart', metadataManagerHandleHeaderDragStart);
  $('metadataManagerHeaderRow').addEventListener('dragover', metadataManagerHandleHeaderDragOver);
  $('metadataManagerHeaderRow').addEventListener('drop', metadataManagerHandleHeaderDrop);
  $('metadataManagerHeaderRow').addEventListener('dragend', metadataManagerHandleHeaderDragEnd);
}
if ($('metadataManagerColumnsReset')) $('metadataManagerColumnsReset').addEventListener('click', e => { e.preventDefault(); metadataManagerResetColumns(); });
if ($('metadataManagerColumnsShowAll')) $('metadataManagerColumnsShowAll').addEventListener('click', e => { e.preventDefault(); metadataManagerShowAllColumns(); });
if ($('metadataManagerTableBody')) $('metadataManagerTableBody').addEventListener('change', e => {
  const target = e.target;
  if (target?.classList?.contains('metadata-manager-row-check')) metadataManagerToggleSelection(target.dataset.id || '', target.checked);
  if (target?.classList?.contains('metadata-manager-input')) metadataManagerMarkDirty(target.dataset.id || '', target.dataset.field || '', target.value);
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
if ($('metadataCoverPicker')) $('metadataCoverPicker').addEventListener('click', handleMetadataCoverPickerClick);
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

