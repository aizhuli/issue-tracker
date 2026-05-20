// Sidebar + top bar
const { useState: useStateS } = React;

function Sidebar({ activeProject, activeView, onProject, onView, density }) {
  const projects = window.MOCK_DATA.projects;
  const navItems = [
    { id: 'inbox',    label: 'Inbox',     icon: 'inbox',   count: 4 },
    { id: 'me',       label: 'My Issues', icon: 'me',      count: 7 },
    { id: 'board',    label: 'Board',     icon: 'board',   active: true },
    { id: 'backlog',  label: 'Backlog',   icon: 'backlog' },
    { id: 'sprints',  label: 'Sprints',   icon: 'sprint' },
  ];
  return (
    <aside className="sidebar">
      <div className="sb-workspace">
        <div className="sb-logo">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none">
            <path d="M4 14L9 4l5 10-5 10z" fill="var(--accent-1)"/>
            <path d="M11 4L16 14l-5 10" stroke="var(--ink-0)" strokeWidth="2.2" strokeLinejoin="round" fill="none"/>
            <circle cx="18" cy="6" r="2.2" fill="var(--ink-0)"/>
          </svg>
          <div>
            <div className="sb-ws-name">Northwind</div>
            <div className="sb-ws-plan">Pro · 14 members</div>
          </div>
        </div>
        <button className="sb-ws-btn" aria-label="Switch workspace">
          <Icon name="down" size={14} />
        </button>
      </div>

      <div className="sb-quick">
        <button className="sb-quick-btn primary">
          <Icon name="plus" size={14} />
          <span>New issue</span>
          <kbd className="kbd">C</kbd>
        </button>
        <button className="sb-quick-btn ghost" aria-label="Search">
          <Icon name="search" size={14} />
        </button>
      </div>

      <nav className="sb-nav">
        {navItems.map(n => (
          <button key={n.id} className={'sb-item' + (n.active ? ' active' : '')}>
            <Icon name={n.icon} size={15} />
            <span>{n.label}</span>
            {n.count != null && <span className="sb-count">{n.count}</span>}
          </button>
        ))}
      </nav>

      <div className="sb-section">
        <div className="sb-section-hd">
          <span>Projects</span>
          <button className="sb-tiny-btn" aria-label="Add project"><Icon name="plus" size={11} /></button>
        </div>
        <div className="sb-projects">
          {projects.map(p => (
            <button
              key={p.id}
              className={'sb-project' + (activeProject === p.id ? ' active' : '')}
              onClick={() => onProject(p.id)}
            >
              <span className="sb-project-mark" style={{ background: p.color }} />
              <span className="sb-project-name">{p.name}</span>
              <span className="sb-project-key">{p.key}</span>
            </button>
          ))}
        </div>
      </div>

      <div className="sb-foot">
        <button className="sb-foot-btn"><Icon name="settings" size={14}/><span>Settings</span></button>
        <button className="sb-foot-btn"><Icon name="help" size={14}/><span>Help</span></button>
      </div>
    </aside>
  );
}

function TopBar({ project, view, onView, query, onQuery, density, onDensity, onOpenAI }) {
  return (
    <header className="topbar">
      <div className="tb-left">
        <div className="tb-crumb">
          <span className="tb-crumb-ws">Northwind</span>
          <Icon name="chevron" size={12} />
          <span className="tb-crumb-proj">
            <span className="tb-crumb-mark" style={{ background: project.color }} />
            {project.name}
          </span>
          <Icon name="chevron" size={12} />
          <span className="tb-crumb-current">Board</span>
        </div>
      </div>
      <div className="tb-right">
        <button className="tb-pill ai" onClick={onOpenAI}>
          <Icon name="sparkle" size={14}/>
          <span>Ask AI</span>
          <kbd className="kbd kbd-dark">⌘K</kbd>
        </button>
        <div className="tb-search">
          <Icon name="search" size={14} color="var(--ink-2)" />
          <input
            placeholder="Search issues…"
            value={query}
            onChange={e => onQuery(e.target.value)}
          />
        </div>
        <button className="tb-icon-btn" aria-label="Notifications">
          <Icon name="bell" size={16}/>
          <span className="tb-dot" />
        </button>
        <Avatar id="u3" size={28} />
      </div>
    </header>
  );
}

function ViewSwitch({ view, onView, count, query, onQuery, onFilters }) {
  return (
    <div className="viewbar">
      <div className="vb-left">
        <div className="vb-tabs">
          {[
            ['board','Board','board'],
            ['list','List','list'],
            ['backlog','Backlog','backlog'],
          ].map(([id,label,icon]) => (
            <button
              key={id}
              className={'vb-tab' + (view === id ? ' active' : '')}
              onClick={() => onView(id)}
            >
              <Icon name={icon} size={13}/>
              {label}
            </button>
          ))}
        </div>
        <span className="vb-divider" />
        <span className="vb-meta">{count} issues</span>
      </div>
      <div className="vb-right">
        <button className="vb-chip"><Icon name="filter" size={12}/>Filter</button>
        <button className="vb-chip"><Icon name="sort" size={12}/>Sort</button>
        <button className="vb-chip"><Icon name="grid" size={12}/>Group</button>
        <span className="vb-divider" />
        <AvatarStack ids={['u1','u2','u3','u4','u5','u6']} size={20} max={5} />
      </div>
    </div>
  );
}

window.Sidebar = Sidebar;
window.TopBar = TopBar;
window.ViewSwitch = ViewSwitch;
