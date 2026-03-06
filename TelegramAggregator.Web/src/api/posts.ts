export interface PostListDto {
  id: number;
  text: string;
  publishedAt: string;
  imageIds: string[];
}

export interface PostPageDto {
  posts: PostListDto[];
  totalCount: number;
  hasMore: boolean;
}

export async function getChannelPosts(
  channelId: number,
  page: number = 1,
  pageSize: number = 20
): Promise<PostPageDto> {
  const response = await fetch(
    `/api/channels/${channelId}/posts?page=${page}&pageSize=${pageSize}`
  );
  if (!response.ok) {
    throw new Error(`Failed to fetch posts: ${response.statusText}`);
  }
  return response.json();
}
