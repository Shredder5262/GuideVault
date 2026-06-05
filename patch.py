from pathlib import Path
root=Path('/mnt/data/gv_user_menu_work/src/PageQuest.Web/wwwroot')
app=root/'app.js'
html=root/'index.html'
css=root/'styles.css'
text=app.read_text()
# Version bump
text=text.replace("const GUIDEVAULT_APP_VERSION = '0.9.17';", "const GUIDEVAULT_APP_VERSION = '0.9.18';")
# Add favorites key
text=text.replace("const GUIDEVAULT_COVER_SIZE_KEY = 'guidevault.libraryCoverSize.v1';", "const GUIDEVAULT_COVER_SIZE_KEY = 'guidevault.libraryCoverSize.v1';\nconst GUIDEVAULT_FAVORITES_KEY = 'guidevault.favorites.v1';")
# Insert favorites functions before normalizeLoginProfile
fav_funcs = r'''
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
'''
if 'function favoriteItemKey' not in text:
    text=text.replace('function normalizeLoginProfile(value = {}) {', fav_funcs+'\nfunction normalizeLoginProfile(value = {}) {')
# Add user menu helpers before renderAccountProfile
user_funcs = r'''
function userInitials(profile = state.auth.profile || readLoginProfile() || {}) {
  const value = String(profile.username || profile.email || 'GV').trim();
  if (!value) return 'GV';
  const parts = value.replace(/@.*$/, '').split(/[\s._-]+/).filter(Boolean);
  if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
  return value.slice(0, 2).toUpperCase();
}
function syncTopUserMenu() {
  const profile = state.auth.profile || readLoginProfile() || {};
  const label = profile.username || profile.email || 'User';
  if ($('topCurrentUser')) $('topCurrentUser').textContent = label;
  if ($('topUserAvatar')) $('topUserAvatar').textContent = userInitials(profile);
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
'''
if 'function syncTopUserMenu' not in text:
    text=text.replace('function renderAccountProfile() {', user_funcs+'\nfunction renderAccountProfile() {')
# Add cleanup reader resources before openReader
cleanup_funcs = r'''
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
function cleanupInactiveViewsForNavigation(nextView = '') {
  if (nextView !== 'reader' && (state.reader?.pages?.length || state.reader?.item)) cleanupReaderResources();
  metadataManagerHandlePreviewPointerEnd?.();
  if (coverPrimeObserver && document.querySelectorAll('img[data-cover-src]').length > 900) {
    try { coverPrimeObserver.disconnect(); } catch {}
    coverPrimeObserver = null;
  }
}
'''
if 'function cleanupReaderResources' not in text:
    text=text.replace('async function openReader(item) {', cleanup_funcs+'\nasync function openReader(item) {')
# Call cleanup at start of openReader
text=text.replace('async function openReader(item) {\n  if (!item) return;', 'async function openReader(item) {\n  if (!item) return;\n  cleanupReaderResources();')
# showDetailScreen cleanup
text=text.replace('function showDetailScreen(item) {\n  if (!item) return;\n  document.body.classList.remove', 'function showDetailScreen(item) {\n  if (!item) return;\n  cleanupInactiveViewsForNavigation(\'detail\');\n  document.body.classList.remove')
# showLibrary / showSettings cleanup
text=text.replace('function showLibraryScreen() {\n  clearColorscapeDetailTheme();', 'function showLibraryScreen() {\n  cleanupInactiveViewsForNavigation(\'library\');\n  clearColorscapeDetailTheme();')
text=text.replace('function showSettingsScreen(tab = \'account\') {\n  clearColorscapeDetailTheme();', 'function showSettingsScreen(tab = \'account\') {\n  cleanupInactiveViewsForNavigation(\'settings\');\n  clearColorscapeDetailTheme();')
# load favorites in auth init
text=text.replace('  loadCategoryStructure();\n  loadLibraryCoverScale();', '  loadCategoryStructure();\n  loadLibraryCoverScale();\n  loadFavorites();')
# sync user menu in showAuthenticatedApp/renderAccountProfile/save
text=text.replace('  renderAccountProfile();\n  startDeviceHeartbeat();', '  renderAccountProfile();\n  syncTopUserMenu();\n  startDeviceHeartbeat();')
text=text.replace("  if ($('accountProfileSummary')) $('accountProfileSummary').textContent = `${profile.username} • ${profile.email}`;\n  setAccountEditMode(false, false);", "  if ($('accountProfileSummary')) $('accountProfileSummary').textContent = `${profile.username} • ${profile.email}`;\n  syncTopUserMenu();\n  setAccountEditMode(false, false);")
text=text.replace('  renderAccountProfile();\n  setAccountStatus(\'Login profile saved.\', \'success\');', '  renderAccountProfile();\n  syncTopUserMenu();\n  setAccountStatus(\'Login profile saved.\', \'success\');')
# apply filter favorites
text=text.replace("    const matchesFilter = state.filter === 'All Content' || item.kind === state.filter;", "    const matchesFilter = state.filter === 'All Content' || (state.filter === 'Favorites' ? isFavoriteItem(item) : item.kind === state.filter);")
# render countFavs
text=text.replace("  $('countFavs').textContent = 0;", "  if (!state.favorites) loadFavorites();\n  $('countFavs').textContent = Object.keys(state.favorites || {}).length;")
# pageTitle favorites
text=text.replace("  return state.filter === 'All Content' ? 'Home' : (state.filter || 'Home');", "  if (state.viewMode === 'favorites' || state.filter === 'Favorites') return 'Favorites';\n  return state.filter === 'All Content' ? 'Home' : (state.filter || 'Home');")
# card favorite markup
old="""function cardMarkupForItem(item) {
  const cover = coverUrl(item);
  return `<article class="card ${specialCardClass(item)} ${state.selected?.id === item.id ? 'selected' : ''}" data-id="${escapeForAttribute(item.id || item.Id || '')}" data-alpha="${alphaKey(displayTitle(item))}">
      <span class="favorite">★</span>
      <div class="cover-wrap"><img decoding="async" loading="lazy" data-cover-src="${cover}" src="/assets/missing-cover.svg" alt="${escapeForAttribute(displayTitle(item))} cover" /></div>"""
new="""function cardMarkupForItem(item) {
  const cover = coverUrl(item);
  const itemId = String(item.id || item.Id || '');
  const favorite = isFavoriteItem(item);
  return `<article class="card ${specialCardClass(item)} ${state.selected?.id === item.id ? 'selected' : ''}" data-id="${escapeForAttribute(itemId)}" data-alpha="${alphaKey(displayTitle(item))}">
      <button class="favorite${favorite ? ' active' : ''}" type="button" data-id="${escapeForAttribute(itemId)}" aria-label="${favorite ? 'Remove from favorites' : 'Add to favorites'}" aria-pressed="${favorite ? 'true' : 'false'}" title="${favorite ? 'Remove from favorites' : 'Add to favorites'}">★</button>
      <div class="cover-wrap"><img decoding="async" loading="lazy" data-cover-src="${cover}" src="/assets/missing-cover.svg" alt="${escapeForAttribute(displayTitle(item))} cover" /></div>"""
if old not in text:
    print('old card snippet not found')
else:
    text=text.replace(old,new)
# Remove per-card listeners / add ignore favorite
text=text.replace("      const card = e.target.closest?.('.card');\n      if (!card || !host.contains(card)) return;", "      if (e.target.closest?.('.favorite')) return;\n      const card = e.target.closest?.('.card');\n      if (!card || !host.contains(card)) return;")
text=text.replace("    const card = e.target.closest?.('article.card[data-id]');\n    if (!card) return;", "    if (e.target.closest?.('.favorite')) return;\n    const card = e.target.closest?.('article.card[data-id]');\n    if (!card) return;")
text=text.replace("  host.querySelectorAll('.card').forEach(card => card.addEventListener('click', () => showDetailScreen(state.items.find(i => String(i.id) === String(card.dataset.id)))));\n  host.querySelectorAll('[data-home-shelf-nav]').forEach(btn => btn.addEventListener('click', handleHomeShelfNavigation));", "  host.querySelectorAll('[data-home-shelf-nav]').forEach(btn => btn.addEventListener('click', handleHomeShelfNavigation));")
text=text.replace("  $(id).querySelectorAll('.card').forEach(card => card.addEventListener('click', () => showDetailScreen(state.items.find(i => String(i.id) === String(card.dataset.id)))));\n}", "}", 1)
# Add event listeners bottom
bottom = """if ($('settingsBtn')) $('settingsBtn').addEventListener('click', () => showSettingsScreen('account'));"""
insert = """document.addEventListener('click', handleFavoriteClick, true);
if ($('userMenuBtn')) $('userMenuBtn').addEventListener('click', e => { e.preventDefault(); e.stopPropagation(); toggleUserMenu(); });
if ($('userMenuProfile')) $('userMenuProfile').addEventListener('click', e => { e.preventDefault(); setUserMenuOpen(false); showSettingsScreen('account'); });
if ($('userMenuHelp')) $('userMenuHelp').addEventListener('click', e => { e.preventDefault(); setUserMenuOpen(false); showSettingsScreen('keybinds'); });
if ($('userMenuLogout')) $('userMenuLogout').addEventListener('click', e => { e.preventDefault(); setUserMenuOpen(false); logoutGuidevault(); });
document.addEventListener('click', e => { if (!$('userMenuPanel')?.classList.contains('hidden') && !e.target.closest?.('.top-user-wrap')) setUserMenuOpen(false); });
"""+bottom
if "if ($('userMenuBtn'))" not in text:
    text=text.replace(bottom, insert)
# Ensure app init calls sync maybe after initialize? fine.
# update history version in system fallback maybe later? no.
app.write_text(text)

# Patch HTML: top user menu
h=html.read_text()
old_top='''        <button id="settingsBtn" class="icon-button top-icon-button" title="Settings" aria-label="Settings">⚙</button>
      </header>'''
new_top='''        <button id="settingsBtn" class="icon-button top-icon-button" title="Settings" aria-label="Settings">⚙</button>
        <div class="top-user-wrap">
          <button id="userMenuBtn" class="top-user-button" type="button" aria-haspopup="menu" aria-expanded="false" title="Guidevault user menu">
            <span id="topUserAvatar" class="top-user-avatar">GV</span>
            <span id="topCurrentUser" class="top-user-name">User</span>
            <span class="top-user-caret" aria-hidden="true">▾</span>
          </button>
          <div id="userMenuPanel" class="top-user-menu hidden" role="menu" aria-label="User menu">
            <button id="userMenuProfile" type="button" role="menuitem">My Profile</button>
            <button id="userMenuHelp" type="button" role="menuitem">Help</button>
            <button id="userMenuLogout" type="button" role="menuitem">Log out</button>
          </div>
        </div>
      </header>'''
if old_top not in h:
    print('topbar snippet not found')
else:
    h=h.replace(old_top,new_top)
# Update history add 0.9.18
h=h.replace('<details class="system-update-entry" open>\n                <summary>\n                  <span>Guidevault 0.9.17 - Login Wallpaper, Category Modes, and Cover Sizing</span>', '<details class="system-update-entry" open>\n                <summary>\n                  <span>Guidevault 0.9.18 - Navigation Performance, Favorites, and User Menu</span>')
h=h.replace('''                <div class="system-update-body">
                  <h3>Changed <b>5</b></h3>
                  <ul>
                    <li>Set the login page to use assets/backgrounds/loginwallpaper.png as the wallpaper background with the normal Guidevault gradient as fallback.</li>
                    <li>Changed the sidebar Categories heading into a browsing-mode dropdown.</li>
                    <li>Added category browsing modes for content type, platform/publication, publisher, and decade.</li>
                    <li>Made sidebar category groups collapsed by default on app start.</li>
                    <li>Added a Cover size slider beside the sort dropdown so library/card cover containers can be scaled up or down.</li>
                  </ul>
                </div>
              </details>''', '''                <div class="system-update-body">
                  <h3>Changed <b>5</b></h3>
                  <ul>
                    <li>Themed native dropdown options so the Categories mode selector no longer opens as a bright default browser menu.</li>
                    <li>Activated the library card favorite star and wired the Favorites side-nav count/filter.</li>
                    <li>Added the current user menu to the upper-right corner with My Profile, Help, and Log out actions.</li>
                    <li>Added reader/navigation cleanup to release page images and reader overlays when switching between books or leaving the reader.</li>
                    <li>Reduced repeated per-card click handlers so grid navigation relies on delegated events instead of accumulating listeners.</li>
                  </ul>
                </div>
              </details>
              <details class="system-update-entry">
                <summary>
                  <span>Guidevault 0.9.17 - Login Wallpaper, Category Modes, and Cover Sizing</span>
                  <em>Installed</em>
                </summary>
                <div class="system-update-body">
                  <h3>Changed <b>5</b></h3>
                  <ul>
                    <li>Set the login page to use assets/backgrounds/loginwallpaper.png as the wallpaper background with the normal Guidevault gradient as fallback.</li>
                    <li>Changed the sidebar Categories heading into a browsing-mode dropdown.</li>
                    <li>Added category browsing modes for content type, platform/publication, publisher, and decade.</li>
                    <li>Made sidebar category groups collapsed by default on app start.</li>
                    <li>Added a Cover size slider beside the sort dropdown so library/card cover containers can be scaled up or down.</li>
                  </ul>
                </div>
              </details>''', 1)
html.write_text(h)

# Append CSS
c=css.read_text()
add=r'''

/* v0.9.18 — category select theme, favorites, user menu, and navigation cleanup */
select,
select option,
.category-structure-select,
.category-structure-select option,
#sort option,
#metadataManagerFocus option,
#metadataManagerType option,
#metadataManagerMissing option,
#metadataManagerCategory option{
  background-color:#101b26;
  color:var(--text);
}
select option:checked,
.category-structure-select option:checked{
  background-color:#1d63d8;
  color:#ffffff;
}
.category-structure-select:focus,
#sort:focus{
  outline:2px solid rgba(88,151,255,.45);
  outline-offset:2px;
  border-color:#58a2ff;
}
.favorite{
  position:absolute;
  right:8px;
  top:8px;
  z-index:3;
  width:30px;
  height:30px;
  display:grid;
  place-items:center;
  border:1px solid rgba(255,255,255,.12);
  border-radius:999px;
  background:rgba(5,10,16,.55);
  color:#758397;
  cursor:pointer;
  text-shadow:none;
  transition:color .14s ease,border-color .14s ease,background .14s ease,transform .14s ease,box-shadow .14s ease;
}
.favorite:hover{
  transform:translateY(-1px);
  color:#ffd86a;
  border-color:rgba(255,216,106,.42);
  background:rgba(18,27,37,.86);
}
.favorite.active{
  color:var(--gold);
  border-color:rgba(255,210,63,.5);
  background:rgba(80,62,11,.72);
  box-shadow:0 0 18px rgba(255,210,63,.18);
}
.top-user-wrap{position:relative;display:flex;align-items:center;margin-left:2px;}
.top-user-button{height:38px;display:flex;align-items:center;gap:9px;border:1px solid var(--line);border-radius:10px;background:rgba(16,27,38,.94);color:var(--text);padding:0 10px 0 7px;cursor:pointer;min-width:0;}
.top-user-button:hover,.top-user-button[aria-expanded="true"]{border-color:#58a2ff;background:#132235;}
.top-user-avatar{width:26px;height:26px;border-radius:999px;display:grid;place-items:center;background:linear-gradient(135deg,#2f81ff,#75b7ff);color:#06111d;font-weight:900;font-size:11px;letter-spacing:.04em;box-shadow:0 0 18px rgba(47,129,255,.24);}
.top-user-name{max-width:170px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;font-weight:700;}
.top-user-caret{color:#9fb5ce;font-size:12px;}
.top-user-menu{position:absolute;right:0;top:44px;min-width:190px;z-index:30;border:1px solid #27445e;border-radius:12px;background:linear-gradient(180deg,#101d2b,#09131e);box-shadow:0 20px 60px rgba(0,0,0,.45);padding:7px;}
.top-user-menu button{width:100%;display:flex;align-items:center;gap:8px;border:0;border-radius:8px;background:transparent;color:var(--text);padding:10px 11px;text-align:left;cursor:pointer;font-weight:700;}
.top-user-menu button:hover{background:rgba(47,129,255,.18);color:#d9ecff;}
@media(max-width:900px){.top-user-name{display:none}.top-user-button{padding-right:8px}.top-user-menu{right:-4px}}
'''
if 'v0.9.18 — category select theme' not in c:
    c += add
css.write_text(c)
