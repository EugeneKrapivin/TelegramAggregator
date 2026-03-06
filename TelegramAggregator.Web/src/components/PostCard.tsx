import { formatDistanceToNow } from 'date-fns';
import type { PostListDto } from '../api/posts';

interface PostCardProps {
  post: PostListDto;
}

export function PostCard({ post }: PostCardProps) {
  const relativeTime = formatDistanceToNow(new Date(post.publishedAt), { addSuffix: true });

  return (
    <div className="border-b border-zinc-800 p-4 hover:bg-zinc-900/50 transition-colors">
      <div className="text-xs text-zinc-500 mb-2" style={{ fontFamily: "'JetBrains Mono', monospace" }}>
        {relativeTime}
      </div>
      
      {post.text && (
        <div className="text-sm text-zinc-300 mb-3 leading-relaxed whitespace-pre-wrap">
          {post.text}
        </div>
      )}

      {post.imageIds.length > 0 && (
        <div className={`grid gap-2 ${
          post.imageIds.length === 1 ? 'grid-cols-1' :
          post.imageIds.length === 2 ? 'grid-cols-2' :
          'grid-cols-2 sm:grid-cols-3'
        }`}>
          {post.imageIds.map((imageId) => (
            <div key={imageId} className="relative aspect-square bg-zinc-900 rounded overflow-hidden">
              <img
                src={`/api/images/${imageId}`}
                alt=""
                loading="lazy"
                className="w-full h-full object-cover"
                onError={(e) => {
                  // Hide broken images
                  (e.target as HTMLImageElement).style.display = 'none';
                }}
              />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
