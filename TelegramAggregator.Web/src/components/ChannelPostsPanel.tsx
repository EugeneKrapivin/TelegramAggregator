import { useState, useEffect, useCallback, useRef } from 'react';
import { getChannelPosts, type PostListDto } from '../api/posts';
import { useClickOutside } from '../hooks/useClickOutside';
import { useInfiniteScroll } from '../hooks/useInfiniteScroll';
import { PostCard } from './PostCard';

interface ChannelPostsPanelProps {
  channelId: number | null;
  channelName: string;
  onClose: () => void;
}

export function ChannelPostsPanel({ channelId, channelName, onClose }: ChannelPostsPanelProps) {
  const [posts, setPosts] = useState<PostListDto[]>([]);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const isInitialLoad = useRef(false);

  const panelRef = useClickOutside<HTMLDivElement>({
    onClickOutside: onClose,
    enabled: channelId !== null,
  });

  const loadMore = useCallback(async () => {
    if (!channelId || loading || !hasMore) return;

    setLoading(true);
    setError(null);

    try {
      const data = await getChannelPosts(channelId, page, 20);
      setPosts((prev) => [...prev, ...data.posts]);
      setHasMore(data.hasMore);
      setPage((p) => p + 1);
    } catch (err) {
      setError(String(err));
    } finally {
      setLoading(false);
    }
  }, [channelId, page, loading, hasMore]);

  const sentinelRef = useInfiniteScroll({
    onLoadMore: loadMore,
    hasMore,
    loading,
  });

  // Reset state when channel changes
  useEffect(() => {
    if (channelId === null) {
      // Clear everything when panel closes
      setPosts([]);
      setPage(1);
      setHasMore(true);
      setError(null);
      isInitialLoad.current = false;
      return;
    }

    // Reset for new channel
    setPosts([]);
    setPage(1);
    setHasMore(true);
    setError(null);
    isInitialLoad.current = true;
  }, [channelId]);

  // Initial load - separate effect to avoid double-loading
  useEffect(() => {
    if (isInitialLoad.current && posts.length === 0 && !loading && channelId !== null) {
      isInitialLoad.current = false;
      loadMore();
    }
  }, [posts.length, loading, channelId, loadMore]);

  // ESC key handler
  useEffect(() => {
    function handleEsc(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        onClose();
      }
    }

    if (channelId !== null) {
      document.addEventListener('keydown', handleEsc);
      return () => document.removeEventListener('keydown', handleEsc);
    }
  }, [channelId, onClose]);

  if (channelId === null) return null;

  return (
    <>
      {/* Backdrop */}
      <div
        className={`fixed inset-0 bg-black/50 z-40 transition-opacity duration-300 ${
          channelId !== null ? 'opacity-100' : 'opacity-0 pointer-events-none'
        }`}
      />

      {/* Panel */}
      <div
        ref={panelRef}
        className={`fixed right-0 top-0 h-full w-full sm:w-[500px] bg-zinc-950 border-l border-zinc-800 z-50 
          transform transition-transform duration-300 ease-out flex flex-col ${
          channelId !== null ? 'translate-x-0' : 'translate-x-full'
        }`}
      >
        {/* Header */}
        <div className="bg-zinc-900 border-b border-zinc-800 px-4 py-3 flex items-center justify-between">
          <div>
            <h2 className="text-lg font-semibold text-zinc-100" style={{ fontFamily: "'Exo 2', sans-serif" }}>
              {channelName}
            </h2>
            <p className="text-xs text-zinc-500">Messages</p>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-zinc-800 rounded-lg transition-colors text-zinc-400 hover:text-zinc-200"
            aria-label="Close"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto">
          {error && (
            <div className="p-4 bg-red-500/10 border-b border-red-500/20 text-red-400 text-sm">
              ⚠️ {error}
            </div>
          )}

          {posts.length === 0 && !loading && !error && (
            <div className="flex items-center justify-center h-64 text-zinc-500">
              <div className="text-center">
                <div className="text-4xl mb-2">📭</div>
                <div>No messages yet</div>
              </div>
            </div>
          )}

          {posts.map((post) => (
            <PostCard key={post.id} post={post} />
          ))}

          {/* Infinite scroll sentinel */}
          <div ref={sentinelRef} className="h-px" />

          {loading && (
            <div className="p-8 flex items-center justify-center">
              <div className="flex items-center gap-2 text-zinc-500">
                <div className="w-4 h-4 border-2 border-zinc-500 border-t-transparent rounded-full animate-spin" />
                <span className="text-sm">Loading...</span>
              </div>
            </div>
          )}

          {!hasMore && posts.length > 0 && (
            <div className="p-4 text-center text-zinc-600 text-sm">
              — End of messages —
            </div>
          )}
        </div>
      </div>
    </>
  );
}
