import { useEffect, useState } from 'react';
import type { Channel, CreateChannelRequest } from '../api';

interface Props {
  channel: Channel | null;   // null = create mode
  onSave: (data: CreateChannelRequest) => void;
  onClose: () => void;
}

export function ChannelModal({ channel, onSave, onClose }: Props) {
  const [telegramChannelId, setTelegramChannelId] = useState('');
  const [username, setUsername] = useState('');
  const [title, setTitle] = useState('');

  useEffect(() => {
    if (channel) {
      setTelegramChannelId(String(channel.telegramChannelId));
      setUsername(channel.username);
      setTitle(channel.title);
    } else {
      setTelegramChannelId('');
      setUsername('');
      setTitle('');
    }
  }, [channel]);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    onSave({ telegramChannelId: Number(telegramChannelId), username, title });
  }

  return (
    <div className="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50">
      <div className="bg-zinc-900 border border-zinc-700 rounded-xl shadow-2xl w-full max-w-md p-6">
        <h2 className="text-base font-semibold text-zinc-100 mb-5">
          {channel ? 'Edit Channel' : 'Add Channel'}
        </h2>
        <form onSubmit={handleSubmit} className="space-y-4">
          <label className="block">
            <span className="text-xs font-medium text-zinc-400 uppercase tracking-wider">Telegram Channel ID</span>
            <input
              type="number"
              required
              disabled={!!channel}
              value={telegramChannelId}
              onChange={e => setTelegramChannelId(e.target.value)}
              className="mt-1.5 block w-full rounded-lg bg-zinc-800 border border-zinc-700 px-3 py-2 text-sm text-zinc-100 placeholder-zinc-600 focus:outline-none focus:ring-1 focus:ring-cyan-500 focus:border-cyan-500 disabled:opacity-40 disabled:cursor-not-allowed font-['JetBrains_Mono']"
            />
          </label>
          <label className="block">
            <span className="text-xs font-medium text-zinc-400 uppercase tracking-wider">Username</span>
            <input
              type="text"
              required
              value={username}
              onChange={e => setUsername(e.target.value)}
              placeholder="@channelname"
              className="mt-1.5 block w-full rounded-lg bg-zinc-800 border border-zinc-700 px-3 py-2 text-sm text-zinc-100 placeholder-zinc-600 focus:outline-none focus:ring-1 focus:ring-cyan-500 focus:border-cyan-500 font-['JetBrains_Mono']"
            />
          </label>
          <label className="block">
            <span className="text-xs font-medium text-zinc-400 uppercase tracking-wider">Title</span>
            <input
              type="text"
              required
              value={title}
              onChange={e => setTitle(e.target.value)}
              className="mt-1.5 block w-full rounded-lg bg-zinc-800 border border-zinc-700 px-3 py-2 text-sm text-zinc-100 placeholder-zinc-600 focus:outline-none focus:ring-1 focus:ring-cyan-500 focus:border-cyan-500"
            />
          </label>
          <div className="flex justify-end gap-2.5 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-lg bg-zinc-800 border border-zinc-700 text-sm text-zinc-300 hover:bg-zinc-700 hover:text-zinc-100 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-4 py-2 rounded-lg bg-cyan-500 text-zinc-950 text-sm font-semibold hover:bg-cyan-400 transition-colors"
            >
              Save
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
