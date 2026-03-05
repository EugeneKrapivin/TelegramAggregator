export interface Channel {
  id: number;
  telegramChannelId: number;
  username: string;
  title: string;
  isActive: boolean;
  addedAt: string;
}

export interface CreateChannelRequest {
  telegramChannelId: number;
  username: string;
  title: string;
}

export interface UpdateChannelRequest {
  username?: string;
  title?: string;
  isActive?: boolean;
}

const BASE = '/api/channels';

export async function getChannels(): Promise<Channel[]> {
  const r = await fetch(BASE);
  if (!r.ok) throw new Error('Failed to fetch channels');
  return r.json();
}

export async function createChannel(req: CreateChannelRequest): Promise<Channel> {
  const r = await fetch(BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  });
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}

export async function updateChannel(id: number, req: UpdateChannelRequest): Promise<Channel> {
  const r = await fetch(`${BASE}/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  });
  if (!r.ok) throw new Error('Failed to update channel');
  return r.json();
}

export async function deleteChannel(id: number): Promise<void> {
  const r = await fetch(`${BASE}/${id}`, { method: 'DELETE' });
  if (!r.ok) throw new Error('Failed to delete channel');
}

export async function getPostCount(channelId: number): Promise<number> {
  const r = await fetch(`${BASE}/${channelId}/posts/count`);
  if (!r.ok) return 0;
  const data = await r.json();
  return data.count;
}
