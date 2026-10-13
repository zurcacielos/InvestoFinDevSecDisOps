<script setup lang="ts">
import { ref, computed } from 'vue';

const props = defineProps<{
  author?: string;
  authorImageUrl?: string;
  isAutomated?: boolean;
  triggerReason?: string;
}>();

const imgFailed = ref(false);

const isBot = computed(() => {
  if (props.isAutomated) return true;
  if (!props.author) return false;
  const lower = props.author.toLowerCase();
  return (
    lower.includes('microsoft.visualstudio.services.tfs') ||
    lower.includes('build service') ||
    lower.includes('automation') ||
    lower.includes('service principal')
  );
});

const parsedName = computed(() => {
  if (isBot.value) {
    let reason = 'CI Run';
    if (props.triggerReason) {
      if (props.triggerReason.toLowerCase().includes('schedule')) reason = 'Scheduled';
      else if (props.triggerReason.toLowerCase().includes('pullrequest')) reason = 'PR Trigger';
      else if (props.triggerReason.toLowerCase().includes('ci')) reason = 'CI Trigger';
    }
    return {
      line1: 'Automated',
      line2: reason,
      fullName: props.author || 'Automated Pipeline Run',
    };
  }

  const raw = props.author?.trim() || 'Unknown';
  const parts = raw.split(/\s+/);

  if (parts.length === 1) {
    return {
      line1: parts[0],
      line2: '',
      fullName: raw,
    };
  }

  return {
    line1: parts[0],
    line2: parts.slice(1).join(' '),
    fullName: raw,
  };
});

const initials = computed(() => {
  if (!props.author) return '?';
  const parts = props.author.trim().split(/\s+/);
  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }
  return parts[0].substring(0, 2).toUpperCase();
});
</script>

<template>
  <div 
    class="inline-flex items-center gap-1.5 min-w-0 max-w-[120px]" 
    :title="parsedName.fullName"
  >
    <!-- Robot Head Icon for Automated Runs -->
    <div 
      v-if="isBot"
      class="w-4 h-4 rounded-full bg-violet-100 text-violet-700 flex items-center justify-center shrink-0 border border-violet-200 shadow-2xs"
      title="Automated Trigger"
    >
      <svg xmlns="http://www.w3.org/2000/svg" class="h-2.5 w-2.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
        <rect x="3" y="11" width="18" height="10" rx="2" />
        <circle cx="12" cy="5" r="2" />
        <path d="M12 7v4" />
        <line x1="8" y1="16" x2="8.01" y2="16" />
        <line x1="16" y1="16" x2="16.01" y2="16" />
      </svg>
    </div>

    <!-- Human Avatar Image -->
    <img 
      v-else-if="authorImageUrl && !imgFailed"
      :src="authorImageUrl"
      @error="imgFailed = true"
      class="w-4 h-4 rounded-full object-cover shrink-0 border border-slate-200"
      :alt="parsedName.fullName"
    />

    <!-- Fallback Initials Badge -->
    <div 
      v-else
      class="w-4 h-4 rounded-full bg-blue-100 text-blue-700 font-bold text-[8px] flex items-center justify-center shrink-0 border border-blue-200"
    >
      {{ initials }}
    </div>

    <!-- 2-Line Name Display -->
    <div class="flex flex-col leading-[1.05] min-w-0 text-left">
      <span class="truncate font-semibold text-slate-700 text-[9px]">{{ parsedName.line1 }}</span>
      <span v-if="parsedName.line2" class="truncate text-slate-400 text-[8px]">{{ parsedName.line2 }}</span>
    </div>
  </div>
</template>
