import type { Channel } from '../api';

interface Props {
  channels: Channel[];
  postCounts: Record<number, number>;
  onToggleActive: (channel: Channel) => void;
  onEdit: (channel: Channel) => void;
  onDelete: (channel: Channel) => void;
}

function PencilIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/>
    </svg>
  );
}

function TrashIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
    </svg>
  );
}

export function ChannelTable({ channels, postCounts, onToggleActive, onEdit, onDelete }: Props) {
  if (channels.length === 0) {
    return (
      <div className="text-center py-16 text-zinc-500">
        <p className="text-sm">No channels yet. Add one above.</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-xl border border-zinc-800">
      <table className="min-w-full text-sm">
        <thead>
          <tr className="border-b border-zinc-800 bg-zinc-900/60">
            {['Title', 'Username', 'Posts', 'Status', 'Added', 'Actions'].map(h => (
              <th key={h} className="px-4 py-3 text-left text-xs font-medium text-zinc-500 uppercase tracking-wider">{h}</th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-800/60">
          {channels.map(ch => (
            <tr
              key={ch.id}
              className={`bg-zinc-900 transition-colors hover:bg-zinc-800/40 ${ch.isActive ? '' : 'opacity-40'}`}
            >
              <td className="px-4 py-3 font-medium text-zinc-100">{ch.title}</td>
              <td className="px-4 py-3 text-zinc-400 font-['JetBrains_Mono'] text-xs">{ch.username}</td>
              <td className="px-4 py-3 text-zinc-300 font-['JetBrains_Mono'] text-xs tabular-nums">
                {postCounts[ch.id] ?? '—'}
              </td>
              <td className="px-4 py-3">
                <button
                  onClick={() => onToggleActive(ch)}
                  className={`px-2.5 py-1 rounded-full text-xs font-medium border transition-colors ${
                    ch.isActive
                      ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/25 hover:bg-emerald-500/20'
                      : 'bg-zinc-800 text-zinc-500 border-zinc-700 hover:bg-zinc-700'
                  }`}
                >
                  {ch.isActive ? '● Active' : '○ Inactive'}
                </button>
              </td>
              <td className="px-4 py-3 text-zinc-500 text-xs font-['JetBrains_Mono']">
                {new Date(ch.addedAt).toLocaleDateString()}
              </td>
              <td className="px-4 py-3">
                <div className="flex items-center gap-1.5">
                  <button
                    onClick={() => onEdit(ch)}
                    title="Edit channel"
                    className="flex items-center justify-center w-7 h-7 rounded-md bg-zinc-800 text-zinc-400 hover:bg-indigo-500/20 hover:text-indigo-400 border border-zinc-700 hover:border-indigo-500/40 transition-colors"
                  >
                    <PencilIcon />
                  </button>
                  <button
                    onClick={() => onDelete(ch)}
                    title="Delete channel"
                    className="flex items-center justify-center w-7 h-7 rounded-md bg-zinc-800 text-zinc-400 hover:bg-red-500/20 hover:text-red-400 border border-zinc-700 hover:border-red-500/40 transition-colors"
                  >
                    <TrashIcon />
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
