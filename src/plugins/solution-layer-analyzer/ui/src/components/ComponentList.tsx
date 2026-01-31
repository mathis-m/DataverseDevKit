import React, { useState, useCallback } from 'react';
import { FixedSizeList as List, ListChildComponentProps } from 'react-window';
import {
  makeStyles,
  tokens,
  Table,
  TableHeader,
  TableRow,
  TableHeaderCell,
  TableBody,
  TableCell,
  Badge,
  Button,
  Text,
  Card,
  useApplyScrollbarWidth,
  Popover,
  PopoverTrigger,
  PopoverSurface,
} from '@fluentui/react-components';
import {
  ChevronRightRegular,
  LayerRegular,
} from '@fluentui/react-icons';
import { ComponentResult } from '../types';
import { useAppStore } from '../store/useAppStore';

interface LayerBadgeInfo {
  index: number;
  solution: string;
  isFirst: boolean;
  isLast: boolean;
  isActive: boolean;
}

/**
 * Determines which solutions should be collapsed based on source/target metadata
 */
function getCollapsedBadges(
  layerSequence: string[],
  sourceSolutions: string[],
  targetSolutions: string[]
): LayerBadgeInfo[] {
  const collapsedBadges: LayerBadgeInfo[] = [];
  const sourceSet = new Set(sourceSolutions.map(s => s.toLowerCase()));
  const targetSet = new Set(targetSolutions.map(s => s.toLowerCase()));

  layerSequence.forEach((solution, i) => {
    const solutionLower = solution.toLowerCase();
    const isFirst = i === 0;
    const isLast = i === layerSequence.length - 1;
    const isActive = solutionLower === 'active';
    const isSource = sourceSet.has(solutionLower);
    const isTarget = targetSet.has(solutionLower);

    // Collapse badges that are not first, last, Active, source, or target
    if (!isFirst && !isLast && !isActive && !isSource && !isTarget) {
      collapsedBadges.push({
        index: i,
        solution,
        isFirst,
        isLast,
        isActive,
      });
    }
  });

  return collapsedBadges;
}

/**
 * Get badge color based on source/target membership
 */
function getBadgeColor(
  badge: LayerBadgeInfo,
  sourceSolutions: string[],
  targetSolutions: string[]
): 'success' | 'danger' | 'warning' | 'brand' {
  const solutionLower = badge.solution.toLowerCase();
  const isSource = sourceSolutions.some(s => s.toLowerCase() === solutionLower);
  const isTarget = targetSolutions.some(s => s.toLowerCase() === solutionLower);
  
  if (isSource) return 'success';
  if (isTarget) return 'danger';
  if (badge.isActive) return 'brand';
  return 'warning';
}

interface LayerSequenceBadgesProps {
  layerSequence: string[];
  sourceSolutions: string[];
  targetSolutions: string[];
  styles: ReturnType<typeof useStyles>;
}

const LayerSequenceBadges: React.FC<LayerSequenceBadgesProps> = ({ 
  layerSequence, 
  sourceSolutions,
  targetSolutions,
  styles 
}) => {
  const [isPopoverOpen, setIsPopoverOpen] = useState(false);
  
  const collapsedBadges = getCollapsedBadges(layerSequence, sourceSolutions, targetSolutions);
  const hasCollapsed = collapsedBadges.length > 1;

  const handlePopoverToggle = useCallback((e: React.MouseEvent) => {
    e.stopPropagation();
    setIsPopoverOpen((prev) => !prev);
  }, []);

  // If only 1 collapsed solution, just show it inline instead of collapsing
  const effectiveCollapsedBadges = hasCollapsed ? collapsedBadges : [];

  // Create render items: either a badge or the collapsed popover
  type RenderItem = 
    | { type: 'badge'; badge: LayerBadgeInfo }
    | { type: 'collapsed'; badges: LayerBadgeInfo[] };

  const renderItems: RenderItem[] = [];
  
  // If no collapsing needed, show all badges in order
  if (!hasCollapsed) {
    layerSequence.forEach((solution, i) => {
      const badge = {
        index: i,
        solution,
        isFirst: i === 0,
        isLast: i === layerSequence.length - 1,
        isActive: solution.toLowerCase() === 'active',
      };
      renderItems.push({ type: 'badge', badge });
    });
  } else {
    // Build items with collapsed group in correct position
    let collapsedInserted = false;
    layerSequence.forEach((solution, i) => {
      const badge = {
        index: i,
        solution,
        isFirst: i === 0,
        isLast: i === layerSequence.length - 1,
        isActive: solution.toLowerCase() === 'active',
      };
      
      const isCollapsed = effectiveCollapsedBadges.some(b => b.index === i);
      
      if (isCollapsed) {
        // Insert the collapsed group once at the first collapsed position
        if (!collapsedInserted) {
          renderItems.push({ type: 'collapsed', badges: effectiveCollapsedBadges });
          collapsedInserted = true;
        }
        // Skip individual collapsed badges
      } else {
        renderItems.push({ type: 'badge', badge });
      }
    });
  }

  return (
    <div className={styles.layerBadges}>
      {renderItems.map((item) => {
        if (item.type === 'badge') {
          return (
            <Badge
              key={item.badge.index}
              appearance="filled"
              color={getBadgeColor(item.badge, sourceSolutions, targetSolutions)}
              size="small"
            >
              {item.badge.index + 1}. {item.badge.solution}
            </Badge>
          );
        } else {
          return (
            <Popover
              key="collapsed"
              open={isPopoverOpen}
              onOpenChange={(_, data) => setIsPopoverOpen(data.open)}
              positioning="below"
            >
              <PopoverTrigger disableButtonEnhancement>
                <Badge
                  appearance="tint"
                  color="informative"
                  size="small"
                  className={styles.collapsedBadge}
                  onClick={handlePopoverToggle}
                >
                  +{item.badges.length} Solutions
                </Badge>
              </PopoverTrigger>
              <PopoverSurface className={styles.popoverSurface}>
                <div className={styles.collapsedList}>
                  {item.badges.map((badge) => (
                    <Badge
                      key={badge.index}
                      appearance="filled"
                      color={getBadgeColor(badge, sourceSolutions, targetSolutions)}
                      size="small"
                    >
                      {badge.index + 1}. {badge.solution}
                    </Badge>
                  ))}
                </div>
              </PopoverSurface>
            </Popover>
          );
        }
      })}
    </div>
  );
};

const useStyles = makeStyles({
  tableContainer: {
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
  },
  clickableRow: {
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  layerBadges: {
    display: 'flex',
    gap: tokens.spacingHorizontalXXS,
    flexWrap: 'wrap',
    alignItems: 'center',
  },
  emptyState: {
    padding: tokens.spacingVerticalXXXL,
    textAlign: 'center',
    color: tokens.colorNeutralForeground3,
  },
  collapsedBadge: {
    cursor: 'pointer',
    ':hover': {
      opacity: 0.8,
    },
  },
  popoverSurface: {
    padding: tokens.spacingVerticalS,
  },
  collapsedList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXXS,
  },
});

interface ComponentListProps {
  components: ComponentResult[];
  onSelectComponent: (component: ComponentResult) => void;
  selectedComponentId?: string | null;
}

interface VirtualizedRowProps extends ListChildComponentProps {
  data: {
    components: ComponentResult[];
    onSelectComponent: (component: ComponentResult) => void;
    selectedComponentId?: string | null;
    styles: ReturnType<typeof useStyles>;
    sourceSolutions: string[];
    targetSolutions: string[];
  };
}

const VirtualizedRow: React.FC<VirtualizedRowProps> = ({ index, style, data }) => {
  const { components, onSelectComponent, selectedComponentId, styles, sourceSolutions, targetSolutions } = data;
  const component = components[index];

  return (
    <TableRow 
      aria-rowindex={index + 2}
      key={component.componentId}
      className={styles.clickableRow}
      onClick={() => onSelectComponent(component)}
      style={{
        ...style,
        backgroundColor: selectedComponentId === component.componentId 
          ? tokens.colorNeutralBackground1Selected 
          : undefined,
      }}
    >
      <TableCell>
        <Badge appearance="outline" color="informative">
          {component.componentType}
        </Badge>
      </TableCell>
      <TableCell>
        <div>
          <Text weight="semibold">{component.displayName || component.logicalName}</Text>
          {component.displayName && (
            <Text size={200} style={{ display: 'block', color: tokens.colorNeutralForeground3 }}>
              {component.logicalName}
            </Text>
          )}
        </div>
      </TableCell>
      <TableCell>
        <Text>{component.displayName ? '-' : ''}</Text>
      </TableCell>
      <TableCell>
        <Text size={200}>{component.tableLogicalName || '-'}</Text>
      </TableCell>
      <TableCell>
        <LayerSequenceBadges 
          layerSequence={component.layerSequence} 
          sourceSolutions={sourceSolutions}
          targetSolutions={targetSolutions}
          styles={styles} 
        />
      </TableCell>
      <TableCell>
        <Badge appearance={component.isManaged ? 'filled' : 'outline'}>
          {component.isManaged ? 'Yes' : 'No'}
        </Badge>
      </TableCell>
      <TableCell>
        <Text size={200}>{component.publisher || '-'}</Text>
      </TableCell>
      <TableCell>
        <Button
          size="small"
          appearance="subtle"
          icon={<ChevronRightRegular />}
          onClick={(e) => {
            e.stopPropagation();
            onSelectComponent(component);
          }}
        />
      </TableCell>
    </TableRow>
  );
};

export const ComponentList: React.FC<ComponentListProps> = ({
  components,
  onSelectComponent,
  selectedComponentId,
}) => {
  const styles = useStyles();
  const appliedScrollbarWidthRef = useApplyScrollbarWidth();
  const { indexMetadata } = useAppStore();
  
  // Get source/target solutions from index metadata
  const sourceSolutions = indexMetadata?.sourceSolutions ?? [];
  const targetSolutions = indexMetadata?.targetSolutions ?? [];

  if (components.length === 0) {
    return (
      <Card>
        <div className={styles.emptyState}>
          <LayerRegular style={{ fontSize: '48px', marginBottom: tokens.spacingVerticalM }} />
          <Text size={500}>No components found</Text>
          <Text size={300}>Try adjusting your filters or index more solutions</Text>
        </div>
      </Card>
    );
  }

  const itemData = {
    components,
    onSelectComponent,
    selectedComponentId,
    styles,
    sourceSolutions,
    targetSolutions,
  };

  return (
    <div className={styles.tableContainer}>
      <Table
        noNativeElements
        aria-label="Components table"
        aria-rowcount={components.length}
        size="small"
      >
        <TableHeader>
          <TableRow aria-rowindex={1}>
            <TableHeaderCell>Type</TableHeaderCell>
            <TableHeaderCell>Logical Name</TableHeaderCell>
            <TableHeaderCell>Display Name</TableHeaderCell>
            <TableHeaderCell>Table</TableHeaderCell>
            <TableHeaderCell>Layer Sequence</TableHeaderCell>
            <TableHeaderCell>Managed</TableHeaderCell>
            <TableHeaderCell>Publisher</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
            {/* Scrollbar alignment for the header */}
            <div role="presentation" ref={appliedScrollbarWidthRef} />
          </TableRow>
        </TableHeader>
        <TableBody>
          <List
            height={500}
            itemCount={components.length}
            itemSize={70}
            width="100%"
            itemData={itemData}
          >
            {VirtualizedRow}
          </List>
        </TableBody>
      </Table>
    </div>
  );
};
