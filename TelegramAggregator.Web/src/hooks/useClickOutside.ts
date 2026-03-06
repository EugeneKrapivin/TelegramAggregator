import { useEffect, useRef } from 'react';

interface UseClickOutsideProps {
  onClickOutside: () => void;
  enabled: boolean;
}

export function useClickOutside<T extends HTMLElement>(
  { onClickOutside, enabled }: UseClickOutsideProps
) {
  const ref = useRef<T>(null);

  useEffect(() => {
    if (!enabled) return;

    function handleClickOutside(event: MouseEvent) {
      if (ref.current && !ref.current.contains(event.target as Node)) {
        onClickOutside();
      }
    }

    // Delay to avoid catching the click that opened the panel
    const timeoutId = setTimeout(() => {
      document.addEventListener('mousedown', handleClickOutside);
    }, 100);

    return () => {
      clearTimeout(timeoutId);
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [onClickOutside, enabled]);

  return ref;
}
