-- Diagnostic queries for image display issue
-- Run these to check database state

-- 1. Check if there are any posts with images
SELECT COUNT(*) as total_posts,
       COUNT(DISTINCT pi.post_id) as posts_with_images
FROM posts p
LEFT JOIN post_images pi ON p.id = pi.post_id;

-- 2. Check if images have content
SELECT COUNT(*) as total_images,
       COUNT(CASE WHEN "Content" IS NOT NULL THEN 1 END) as images_with_content,
       COUNT(CASE WHEN "Content" IS NULL THEN 1 END) as images_without_content,
       AVG(LENGTH("Content")) as avg_content_size
FROM images;

-- 3. Sample: Get a post with its image IDs
SELECT p."Id", LEFT(p."Text", 50) as text_preview, 
       ARRAY_AGG(pi."ImageId") as image_ids
FROM posts p
LEFT JOIN post_images pi ON p."Id" = pi."PostId"
WHERE p."Id" IN (SELECT "Id" FROM posts LIMIT 5)
GROUP BY p."Id", p."Text";

-- 4. Check specific images
SELECT "Id", "MimeType", "Width", "Height", 
       CASE WHEN "Content" IS NULL THEN 'NULL' ELSE 'HAS CONTENT' END as content_status,
       LENGTH("Content") as content_size_bytes,
       "AddedAt"
FROM images
ORDER BY "AddedAt" DESC
LIMIT 10;

-- 5. Check if any post_images associations exist
SELECT COUNT(*) as total_associations,
       COUNT(DISTINCT "PostId") as unique_posts,
       COUNT(DISTINCT "ImageId") as unique_images
FROM post_images;
