// Right-side issue detail drawer + AI quick create command bar.
const { useState: useStateD, useEffect: useEffectD, useRef: useRefD } = React;

function IssueDrawer({ issue, onClose, onUpdate }) {
  const [tab, setTab] = useStateD('comments');
  useEffectD(() => {
    const onKey = (e) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);
  if (!issue) return null;

  const assignee = window.MEMBERS_BY_ID[issue.assignee];

  const activity = [
    { who: 'u2', text: 'opened this issue', when: '4 days ago' },
    { who: 'u3', text: 'changed priority to High', when: '3 days ago' },
    { who: 'u2', text: 'moved to In Progress', when: '12 minutes ago' },
  ];
  const comments = [
    { who: 'u3', when: '2d', body: 'Should the drawer support keyboard navigation? J/K between issues would be nice.' },
    { who: 'u2', when: '1d', body: 'Yes — already wired up via WEB-133. Want the same shortcuts here.' },
    { who: 'u6', when: '4h', body: 'Pushed a small spec on the Comments tab — let me know if anything is unclear.' },
  ];

  return (
    <>
      <div className="drawer-scrim" onClick={onClose}/>
      <aside className="drawer" role="dialog" aria-label="Issue detail">
        <header className="dr-hd">
          <div className="dr-hd-l">
            <span className="dr-id">{issue.id}</span>
            <button className="dr-status-pill">
              <StatusDot id={issue.status} size={9}/>
              <span>{window.STATUSES.find(s => s.id === issue.status).label}</span>
              <Icon name="down" size={11}/>
            </button>
          </div>
          <div className="dr-hd-r">
            <button className="dr-icon-btn" title="Copy link"><Icon name="link" size={14}/></button>
            <button className="dr-icon-btn" title="More"><Icon name="dots" size={14}/></button>
            <button className="dr-icon-btn" onClick={onClose} title="Close"><Icon name="close" size={14}/></button>
          </div>
        </header>

        <div className="dr-body">
          <div className="dr-main">
            <h1 className="dr-title" contentEditable suppressContentEditableWarning>{issue.title}</h1>

            <div className="dr-ai-card">
              <div className="dr-ai-hd">
                <Icon name="sparkle" size={13}/>
                <span>AI summary</span>
                <span className="dr-ai-badge">beta</span>
              </div>
              <p className="dr-ai-body">
                Build a right-side drawer that opens when an issue card is clicked. The drawer
                should surface comments, an activity log and outbound links. {comments.length} comments so far;
                consensus is to mirror the J/K keyboard pattern from <span className="dr-ai-ref">WEB-133</span>.
              </p>
            </div>

            <div className="dr-desc">
              <p>{issue.desc || 'Add a description. Type / for commands, @ to mention, # to link an issue.'}</p>
              <p>The drawer is roughly 480px wide on desktop and slides in from the right with a soft scrim behind it. Closing dismisses with Esc or by clicking outside.</p>
              <ul>
                <li>Comments tab with markdown support</li>
                <li>Activity log scoped to this issue</li>
                <li>Linked issues, branches, PRs</li>
              </ul>
            </div>

            <div className="dr-tabs">
              {['comments','activity','links'].map(t => (
                <button key={t} className={'dr-tab' + (tab === t ? ' active' : '')} onClick={() => setTab(t)}>
                  {t === 'comments' ? 'Comments' : t === 'activity' ? 'Activity' : 'Links'}
                  <span className="dr-tab-count">
                    {t === 'comments' ? comments.length : t === 'activity' ? activity.length : 2}
                  </span>
                </button>
              ))}
            </div>

            {tab === 'comments' && (
              <div className="dr-comments">
                {comments.map((c, i) => (
                  <div key={i} className="dr-comment">
                    <Avatar id={c.who} size={28}/>
                    <div className="dr-comment-body">
                      <div className="dr-comment-hd">
                        <strong>{window.MEMBERS_BY_ID[c.who].name}</strong>
                        <span>{c.when} ago</span>
                      </div>
                      <p>{c.body}</p>
                    </div>
                  </div>
                ))}
                <div className="dr-comment-compose">
                  <Avatar id="u3" size={28}/>
                  <div className="dr-compose-box">
                    <div className="dr-compose-input" contentEditable suppressContentEditableWarning data-placeholder="Leave a comment…"></div>
                    <div className="dr-compose-foot">
                      <div className="dr-compose-tools">
                        <button title="Attach"><Icon name="paperclip" size={13}/></button>
                        <button title="Mention">@</button>
                        <button title="AI"><Icon name="sparkle" size={13}/></button>
                      </div>
                      <button className="dr-compose-send">Comment</button>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {tab === 'activity' && (
              <div className="dr-activity">
                {activity.map((a, i) => (
                  <div key={i} className="dr-act-row">
                    <Avatar id={a.who} size={20}/>
                    <span><strong>{window.MEMBERS_BY_ID[a.who].name}</strong> {a.text}</span>
                    <span className="dr-act-when">{a.when}</span>
                  </div>
                ))}
              </div>
            )}

            {tab === 'links' && (
              <div className="dr-links">
                <div className="dr-link-row">
                  <Icon name="branch" size={13}/>
                  <span className="dr-link-name">feat/web-136-issue-drawer</span>
                  <span className="dr-link-meta">3 commits · open</span>
                </div>
                <div className="dr-link-row">
                  <Icon name="link" size={13}/>
                  <span className="dr-link-name">Figma — Drawer v3</span>
                  <span className="dr-link-meta">edited 2h ago</span>
                </div>
              </div>
            )}
          </div>

          <aside className="dr-side">
            <DrawerField label="Status">
              <button className="dr-field-btn">
                <StatusDot id={issue.status} size={9}/>
                {window.STATUSES.find(s => s.id === issue.status).label}
              </button>
            </DrawerField>
            <DrawerField label="Assignee">
              <button className="dr-field-btn">
                <Avatar id={issue.assignee} size={18}/>
                {assignee?.name}
              </button>
            </DrawerField>
            <DrawerField label="Priority">
              <button className="dr-field-btn">
                <PriorityGlyph level={issue.priority} size={11}/>
                {window.PRIORITIES[issue.priority].label}
              </button>
            </DrawerField>
            <DrawerField label="Estimate">
              <button className="dr-field-btn">
                <span className="dr-pts">{issue.points}</span>
                points
              </button>
            </DrawerField>
            <DrawerField label="Labels">
              <div className="dr-labels">
                {issue.labels.map(l => <Label key={l} text={l}/>)}
                <button className="dr-add-label"><Icon name="plus" size={10}/></button>
              </div>
            </DrawerField>
            <DrawerField label="Due">
              <button className="dr-field-btn"><Icon name="calendar" size={12}/>May 23</button>
            </DrawerField>
            <DrawerField label="Sprint">
              <button className="dr-field-btn"><Icon name="sprint" size={12}/>Sprint 24 · Wk 21</button>
            </DrawerField>
            <DrawerField label="Project">
              <button className="dr-field-btn">
                <span className="dr-mark" style={{ background: '#7AB85E' }}/>
                Web Platform
              </button>
            </DrawerField>

            <div className="dr-side-foot">
              <div>Created <strong>{issue.created}</strong></div>
              <div>Updated <strong>{issue.updated} ago</strong></div>
            </div>
          </aside>
        </div>
      </aside>
    </>
  );
}

function DrawerField({ label, children }) {
  return (
    <div className="dr-field">
      <div className="dr-field-lbl">{label}</div>
      <div className="dr-field-val">{children}</div>
    </div>
  );
}

function AICommand({ open, onClose, onCreate }) {
  const [value, setValue] = useStateD('');
  const inputRef = useRefD(null);
  useEffectD(() => {
    if (open) setTimeout(() => inputRef.current?.focus(), 30);
  }, [open]);
  useEffectD(() => {
    const onKey = (e) => {
      if ((e.key === 'k' || e.key === 'K') && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        if (open) onClose(); else { /* parent toggles */ }
      }
      if (e.key === 'Escape' && open) onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  // naive parse for the preview
  const parsed = parseNL(value);

  if (!open) return null;
  return (
    <>
      <div className="ai-scrim" onClick={onClose}/>
      <div className="ai-modal" role="dialog">
        <div className="ai-hd">
          <Icon name="sparkle" size={14}/>
          <input
            ref={inputRef}
            placeholder="Describe an issue — e.g. “Bug: search returns 500 on empty query, high priority, assign Theo”"
            value={value}
            onChange={e => setValue(e.target.value)}
            onKeyDown={e => { if (e.key === 'Enter' && value.trim()) { onCreate(parsed); onClose(); } }}
          />
          <kbd className="kbd kbd-dark">esc</kbd>
        </div>
        {value.trim() && (
          <div className="ai-preview">
            <div className="ai-preview-lbl">AI will create:</div>
            <div className="ai-card-preview">
              <div className="card-top">
                <span className="card-id">WEB-···</span>
                <PriorityGlyph level={parsed.priority} size={12}/>
              </div>
              <div className="card-title">{parsed.title}</div>
              <div className="card-labels">
                {parsed.labels.map(l => <Label key={l} text={l}/>)}
              </div>
              <div className="card-bottom">
                <div className="card-meta">
                  <span className="card-points">{parsed.points}</span>
                  <span className="card-stat dim"><Icon name="branch" size={11}/></span>
                </div>
                <Avatar id={parsed.assignee} size={20}/>
              </div>
            </div>
            <div className="ai-chips">
              <span className="ai-chip">
                <StatusDot id={parsed.status} size={8}/>
                {window.STATUSES.find(s => s.id === parsed.status).label}
              </span>
              <span className="ai-chip">
                <PriorityGlyph level={parsed.priority} size={10}/>
                {window.PRIORITIES[parsed.priority].label}
              </span>
              <span className="ai-chip">
                <Avatar id={parsed.assignee} size={14}/>
                {window.MEMBERS_BY_ID[parsed.assignee].name}
              </span>
              <span className="ai-chip">{parsed.points} pts</span>
            </div>
          </div>
        )}
        <div className="ai-foot">
          <div className="ai-foot-l">
            <span><kbd className="kbd">↵</kbd> Create</span>
            <span><kbd className="kbd">⇧↵</kbd> Create + open</span>
            <span><kbd className="kbd">⌥↵</kbd> Add to backlog</span>
          </div>
          <button
            className="ai-cta"
            disabled={!value.trim()}
            onClick={() => { onCreate(parsed); onClose(); }}
          >
            Create issue
            <Icon name="enter" size={13}/>
          </button>
        </div>
      </div>
    </>
  );
}

function parseNL(text) {
  const t = text.toLowerCase();
  const priority =
    /\b(urgent|p0)\b/.test(t) ? 'urgent' :
    /\b(high|p1|important)\b/.test(t) ? 'high' :
    /\b(low|p3|minor)\b/.test(t) ? 'low' : 'medium';
  const labels = [];
  if (/\bbug\b/.test(t)) labels.push('bug');
  if (/\bdesign\b/.test(t)) labels.push('design');
  if (/\bperf|performance/.test(t)) labels.push('perf');
  if (/\bai\b/.test(t)) labels.push('ai');
  if (/\baccessib|a11y/.test(t)) labels.push('a11y');
  if (labels.length === 0) labels.push('triage');
  const members = window.MOCK_DATA.members;
  let assignee = 'u3';
  for (const m of members) {
    if (t.includes(m.name.toLowerCase().split(' ')[0])) { assignee = m.id; break; }
  }
  const status = /\bbacklog\b/.test(t) ? 'backlog' : 'todo';
  return {
    title: text.replace(/^\s*[a-z]+\s*:\s*/i, '').replace(/,?\s*(high|low|medium|urgent|p[0-3])\s*priority/i,'').replace(/,?\s*assign\s+\w+/i,'').replace(/^./, c => c.toUpperCase()).trim(),
    priority,
    labels,
    assignee,
    status,
    points: labels.includes('bug') ? 3 : 5,
  };
}

window.IssueDrawer = IssueDrawer;
window.AICommand = AICommand;
