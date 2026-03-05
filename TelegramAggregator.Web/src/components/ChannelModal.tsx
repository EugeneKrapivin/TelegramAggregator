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
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md p-6">
        <h2 className="text-lg font-semibold mb-4">
          {channel ? 'Edit Channel' : 'Add Channel'}
        </h2>
        <form onSubmit={handleSubmit} className="space-y-4">
          <label className="block">
            <span className="text-sm font-medium text-gray-700">Telegram Channel ID</span>
            <input
              type="number"
              required
              disabled={!!channel}
              value={telegramChannelId}
              onChange={e => setTelegramChannelId(e.target.value)}
              className="mt-1 block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:bg-gray-100"
            />
          </label>
          <label className="block">
            <span className="text-sm font-medium text-gray-700">Username</span>
            <input
              type="text"
              required
              value={username}
              onChange={e => setUsername(e.target.value)}
              placeholder="@channelname"
              className="mt-1 block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </label>
          <label className="block">
            <span className="text-sm font-medium text-gray-700">Title</span>
            <input
              type="text"
              required
              value={title}
              onChange={e => setTitle(e.target.value)}
              className="mt-1 block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </label>
          <div className="flex justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-lg border border-gray-300 text-sm hover:bg-gray-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-4 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700"
            >
              Save
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
