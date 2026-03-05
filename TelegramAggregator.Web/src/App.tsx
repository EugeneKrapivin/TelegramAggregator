import { useEffect, useState, useCallback } from 'react';
import type { Channel, CreateChannelRequest } from './api';
import { getChannels, createChannel, updateChannel, deleteChannel, getPostCount } from './api';
import { ChannelTable } from './components/ChannelTable';
import { ChannelModal } from './components/ChannelModal';

export default function App() {
  const [channels, setChannels] = useState<Channel[]>([]);
  const [postCounts, setPostCounts] = useState<Record<number, number>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [modalChannel, setModalChannel] = useState<Channel | null | undefined>(undefined);
  // undefined = closed, null = create mode, Channel = edit mode
  const [deleteTarget, setDeleteTarget] = useState<Channel | null>(null);

  const loadChannels = useCallback(async () => {
    try {
      setError(null);
      const data = await getChannels();
      setChannels(data);
      // Load post counts in parallel
      const counts = await Promise.all(data.map(ch => getPostCount(ch.id).then(c => [ch.id, c] as const)));
      setPostCounts(Object.fromEntries(counts));
    } catch (e) {
      setError(String(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadChannels(); }, [loadChannels]);

  async function handleSave(req: CreateChannelRequest) {
    try {
      if (modalChannel) {
        await updateChannel(modalChannel.id, { username: req.username, title: req.title });
      } else {
        await createChannel(req);
      }
      setModalChannel(undefined);
      await loadChannels();
    } catch (e) {
      setError(String(e));
    }
  }

  async function handleToggleActive(ch: Channel) {
    try {
      await updateChannel(ch.id, { isActive: !ch.isActive });
      await loadChannels();
    } catch (e) {
      setError(String(e));
    }
  }

  async function handleDelete() {
    if (!deleteTarget) return;
    try {
      await deleteChannel(deleteTarget.id);
      setDeleteTarget(null);
      await loadChannels();
    } catch (e) {
      setError(String(e));
    }
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white border-b border-gray-200 px-6 py-4 flex items-center justify-between">
        <h1 className="text-xl font-bold text-gray-900">Telegram Aggregator</h1>
        <button
          onClick={() => setModalChannel(null)}
          className="px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700"
        >
          + Add Channel
        </button>
      </header>

      <main className="max-w-5xl mx-auto px-6 py-8">
        {error && (
          <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg text-red-700 text-sm">
            {error}
          </div>
        )}

        {loading ? (
          <p className="text-center text-gray-400 py-16">Loading…</p>
        ) : (
          <ChannelTable
            channels={channels}
            postCounts={postCounts}
            onToggleActive={handleToggleActive}
            onEdit={ch => setModalChannel(ch)}
            onDelete={ch => setDeleteTarget(ch)}
          />
        )}
      </main>

      {modalChannel !== undefined && (
        <ChannelModal
          channel={modalChannel}
          onSave={handleSave}
          onClose={() => setModalChannel(undefined)}
        />
      )}

      {deleteTarget && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
          <div className="bg-white rounded-xl shadow-xl w-full max-w-sm p-6">
            <h2 className="text-lg font-semibold mb-2">Delete channel?</h2>
            <p className="text-sm text-gray-600 mb-6">
              <strong>{deleteTarget.title}</strong> will be permanently removed.
            </p>
            <div className="flex justify-end gap-3">
              <button
                onClick={() => setDeleteTarget(null)}
                className="px-4 py-2 rounded-lg border border-gray-300 text-sm hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                onClick={handleDelete}
                className="px-4 py-2 rounded-lg bg-red-600 text-white text-sm font-medium hover:bg-red-700"
              >
                Delete
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
