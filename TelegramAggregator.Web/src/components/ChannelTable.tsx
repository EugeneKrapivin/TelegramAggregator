import type { Channel } from '../api';

interface Props {
  channels: Channel[];
  postCounts: Record<number, number>;
  onToggleActive: (channel: Channel) => void;
  onEdit: (channel: Channel) => void;
  onDelete: (channel: Channel) => void;
}

export function ChannelTable({ channels, postCounts, onToggleActive, onEdit, onDelete }: Props) {
  if (channels.length === 0) {
    return <p className="text-center text-gray-500 py-8">No channels yet. Add one above.</p>;
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200">
      <table className="min-w-full divide-y divide-gray-200 text-sm">
        <thead className="bg-gray-50">
          <tr>
            {['Title', 'Username', 'Posts', 'Active', 'Added', 'Actions'].map(h => (
              <th key={h} className="px-4 py-3 text-left font-medium text-gray-600">{h}</th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100 bg-white">
          {channels.map(ch => (
            <tr key={ch.id} className={ch.isActive ? '' : 'opacity-50'}>
              <td className="px-4 py-3 font-medium text-gray-900">{ch.title}</td>
              <td className="px-4 py-3 text-gray-500">{ch.username}</td>
              <td className="px-4 py-3 text-gray-700">{postCounts[ch.id] ?? '—'}</td>
              <td className="px-4 py-3">
                <button
                  onClick={() => onToggleActive(ch)}
                  className={`px-2 py-1 rounded text-xs font-semibold ${
                    ch.isActive
                      ? 'bg-green-100 text-green-700 hover:bg-green-200'
                      : 'bg-gray-100 text-gray-500 hover:bg-gray-200'
                  }`}
                >
                  {ch.isActive ? 'Active' : 'Inactive'}
                </button>
              </td>
              <td className="px-4 py-3 text-gray-400 text-xs">
                {new Date(ch.addedAt).toLocaleDateString()}
              </td>
              <td className="px-4 py-3 flex gap-2">
                <button
                  onClick={() => onEdit(ch)}
                  className="text-blue-600 hover:text-blue-800 text-xs font-medium"
                >
                  Edit
                </button>
                <button
                  onClick={() => onDelete(ch)}
                  className="text-red-600 hover:text-red-800 text-xs font-medium"
                >
                  Delete
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
