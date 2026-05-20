// Kanban board with drag/drop between columns.
const { useState: useStateB, useRef: useRefB } = React;

function IssueCard({ issue, onClick, dragHandlers, density }) {
  const compact = density === 'compact';
  return (
    <div
      className={'card' + (compact ? ' compact' : '')}
      draggable
      onClick={onClick}
      {...dragHandlers}
    >
      <div className="card-top">
        <span className="card-id">{issue.id}</span>
        <PriorityGlyph level={issue.priority} size={12} />
      </div>
      <div className="card-title">{issue.title}</div>
      {!compact && issue.labels && issue.labels.length > 0 && (
        <div className="card-labels">
          {issue.labels.map(l => <Label key={l} text={l} />)}
        </div>
      )}
      <div className="card-bottom">
        <div className="card-meta">
          {issue.points != null && (
            <span className="card-points" title={issue.points + ' story points'}>
              {issue.points}
            </span>
          )}
          {issue.comments > 0 && (
            <span className="card-stat">
              <Icon name="comment" size={11}/>
              {issue.comments}
            </span>
          )}
          <span className="card-stat dim">
            <Icon name="branch" size={11}/>
          </span>
        </div>
        <Avatar id={issue.assignee} size={20} />
      </div>
    </div>
  );
}

function KanbanColumn({ status, issues, onDropTo, onCardClick, draggedId, setDraggedId, density }) {
  const [dragOver, setDragOver] = useStateB(false);
  const limits = { todo: null, in_progress: 4, in_review: 3, backlog: null, done: null };
  const wip = limits[status.id];
  const over = wip != null && issues.length > wip;
  return (
    <div
      className={'col' + (dragOver ? ' drag-over' : '')}
      onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
      onDragLeave={() => setDragOver(false)}
      onDrop={(e) => {
        e.preventDefault();
        setDragOver(false);
        if (draggedId) onDropTo(draggedId, status.id);
      }}
    >
      <div className="col-hd">
        <div className="col-hd-l">
          <StatusDot id={status.id} size={10} />
          <span className="col-name">{status.label}</span>
          <span className="col-count">{issues.length}</span>
        </div>
        <div className="col-hd-r">
          {wip != null && (
            <span className={'col-wip' + (over ? ' over' : '')}>{issues.length}/{wip}</span>
          )}
          <button className="col-add" aria-label="Add issue"><Icon name="plus" size={12}/></button>
          <button className="col-add" aria-label="Column menu"><Icon name="dots" size={12}/></button>
        </div>
      </div>
      <div className="col-body">
        {issues.map(i => (
          <IssueCard
            key={i.id}
            issue={i}
            density={density}
            onClick={() => onCardClick(i.id)}
            dragHandlers={{
              onDragStart: (e) => {
                setDraggedId(i.id);
                e.dataTransfer.effectAllowed = 'move';
              },
              onDragEnd: () => setDraggedId(null),
            }}
          />
        ))}
        <button className="col-add-card">
          <Icon name="plus" size={12}/>
          <span>New issue</span>
        </button>
      </div>
    </div>
  );
}

function KanbanBoard({ issues, onMove, onCardClick, density }) {
  const [draggedId, setDraggedId] = useStateB(null);
  const grouped = {};
  for (const s of window.STATUSES) grouped[s.id] = [];
  for (const i of issues) (grouped[i.status] || (grouped[i.status] = [])).push(i);
  return (
    <div className="board">
      {window.STATUSES.map(s => (
        <KanbanColumn
          key={s.id}
          status={s}
          issues={grouped[s.id] || []}
          onDropTo={onMove}
          onCardClick={onCardClick}
          draggedId={draggedId}
          setDraggedId={setDraggedId}
          density={density}
        />
      ))}
    </div>
  );
}

function ListView({ issues, onCardClick }) {
  const grouped = {};
  for (const s of window.STATUSES) grouped[s.id] = [];
  for (const i of issues) (grouped[i.status] || (grouped[i.status] = [])).push(i);
  return (
    <div className="list">
      {window.STATUSES.map(s => {
        const rows = grouped[s.id] || [];
        if (rows.length === 0) return null;
        return (
          <section key={s.id} className="list-group">
            <header className="list-group-hd">
              <StatusDot id={s.id} size={10}/>
              <span className="list-group-name">{s.label}</span>
              <span className="list-group-count">{rows.length}</span>
              <span className="list-group-line" />
            </header>
            {rows.map(i => (
              <div key={i.id} className="row" onClick={() => onCardClick(i.id)}>
                <PriorityGlyph level={i.priority} size={12} />
                <span className="row-id">{i.id}</span>
                <span className="row-title">{i.title}</span>
                <div className="row-labels">
                  {(i.labels || []).slice(0,2).map(l => <Label key={l} text={l}/>)}
                </div>
                <span className="row-points">{i.points}</span>
                <span className="row-comments">
                  {i.comments > 0 && (<><Icon name="comment" size={11}/>{i.comments}</>)}
                </span>
                <span className="row-updated">{i.updated}</span>
                <Avatar id={i.assignee} size={20} />
              </div>
            ))}
          </section>
        );
      })}
    </div>
  );
}

window.IssueCard = IssueCard;
window.KanbanBoard = KanbanBoard;
window.ListView = ListView;
