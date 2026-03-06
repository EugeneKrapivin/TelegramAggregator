import { useEffect, useState, useCallback } from 'react';
import type { Channel, CreateChannelRequest } from '../api';
import { getChannels, createChannel, updateChannel, deleteChannel, getPostCount } from '../api';
import { ChannelTable } from '../components/ChannelTable';
import { ChannelModal } from '../components/ChannelModal';
import { ChannelPostsPanel } from '../components/ChannelPostsPanel';

export default function ChannelsPage() {
  const [channels, setChannels] = useState<Channel[]>([]);
  const [postCounts, setPostCounts] = useState<Record<number, number>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [modalChannel, setModalChannel] = useState<Channel | null | undefined>(undefined);
  // undefined = closed, null = create mode, Channel = edit mode
  const [deleteTarget, setDeleteTarget] = useState<Channel | null>(null);
  const [selectedChannel, setSelectedChannel] = useState<Channel | null>(null);

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
    <div className="min-h-screen bg-zinc-950">
      <header className="bg-zinc-900 border-b border-zinc-800 px-6 py-4 flex items-center justify-between">
        <h1
          className="text-lg font-semibold text-zinc-100 tracking-tight"
          style={{ fontFamily: "'Exo 2', sans-serif" }}
        >
          Telegram Aggregator
        </h1>
        <div className="flex items-center gap-3">
          <a
            href="/settings/telegram-login"
            className="px-3.5 py-1.5 bg-zinc-800 border border-zinc-700 text-zinc-300 text-sm font-semibold rounded-lg hover:bg-zinc-700 transition-colors"
          >
            ⚙️ Telegram Login
          </a>
          <button
            onClick={() => setModalChannel(null)}
            className="px-3.5 py-1.5 bg-cyan-500 text-zinc-950 text-sm font-semibold rounded-lg hover:bg-cyan-400 transition-colors"
          >
            + Add Channel
          </button>
        </div>
      </header>

      <main className="max-w-5xl mx-auto px-6 py-8">
        {error && (
          <div className="mb-4 p-3 bg-red-500/10 border border-red-500/25 rounded-lg text-red-400 text-sm">
            {error}
          </div>
        )}

        {loading ? (
          <p className="text-center text-zinc-600 py-16 text-sm">Loading…</p>
        ) : (
          <ChannelTable
            channels={channels}
            postCounts={postCounts}
            onToggleActive={handleToggleActive}
            onEdit={ch => setModalChannel(ch)}
            onDelete={ch => setDeleteTarget(ch)}
            onRowClick={ch => setSelectedChannel(ch)}
          />
        )}
      </main>

      <ChannelPostsPanel
        channelId={selectedChannel?.id ?? null}
        channelName={selectedChannel?.title || selectedChannel?.username || ''}
        onClose={() => setSelectedChannel(null)}
      />

      {modalChannel !== undefined && (
        <ChannelModal
          channel={modalChannel}
          onSave={handleSave}
          onClose={() => setModalChannel(undefined)}
        />
      )}

      {deleteTarget && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-zinc-900 border border-zinc-700 rounded-xl shadow-2xl w-full max-w-sm p-6">
            <h2 className="text-base font-semibold text-zinc-100 mb-2">Delete channel?</h2>
            <p className="text-sm text-zinc-400 mb-6">
              <span className="text-zinc-200 font-medium">{deleteTarget.title}</span> will be permanently removed.
            </p>
            <div className="flex justify-end gap-2.5">
              <button
                onClick={() => setDeleteTarget(null)}
                className="px-4 py-2 rounded-lg bg-zinc-800 border border-zinc-700 text-sm text-zinc-300 hover:bg-zinc-700 hover:text-zinc-100 transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={handleDelete}
                className="px-4 py-2 rounded-lg bg-red-500 text-white text-sm font-semibold hover:bg-red-400 transition-colors"
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
